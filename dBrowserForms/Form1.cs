using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp.WinForms;
using CefSharp;

namespace dBrowserForms
{
    public partial class Form1 : Form
    {
        ChromiumWebBrowser browser = null;

        Queue<string> orders = null;

        DateTime? waitinguntil = null;
        bool processing = false;
        bool autoquit = false;
        bool defaultautoquit = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (Environment.GetCommandLineArgs() != null)
            {
                string[] args = Environment.GetCommandLineArgs();

                foreach (string arg in args)
                {
                    if (arg.Contains("=") && !arg.StartsWith("=") && !arg.EndsWith("="))
                    {
                        switch (arg.Split(new string[] { "=" }, StringSplitOptions.None)[0])
                        {
                            case "script":
                                EnqueueOrders(arg.Split(new string[] { "=" }, StringSplitOptions.None)[1]);
                                break;
                            case "autoquit":
                                string value = arg.Split(new string[] { "=" }, StringSplitOptions.None)[1];

                                switch (value)
                                {
                                    case "true":
                                        autoquit = true;
                                        break;
                                    case "false":
                                        autoquit = false;
                                        break;
                                    case "default":
                                        autoquit = defaultautoquit;
                                        break;
                                }
                                break;
                            default:
                                orders.Enqueue(arg);
                                break;
                        }
                    }
                }
            }

            if (Cef.IsInitialized)
            {
                Cef.Shutdown();
            }

            Cef.Initialize(new CefSettings() { IgnoreCertificateErrors = true });

            browser = new ChromiumWebBrowser("");
            
            panel1.Controls.Add(browser);
            browser.Dock = DockStyle.Fill;
            orders = new Queue<string>();
            timer1.Enabled = true;
        }

        private void EnqueueOrders(string filename)
        {
            if (System.IO.File.Exists(filename))
            {
                System.IO.StreamReader reader = new System.IO.StreamReader(filename);
                string text = reader.ReadToEnd();
                reader.Close();
                reader.Dispose();

                string[] lines = text.Split(new string[] { @"\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    orders.Enqueue(line);
                }
            }
        }

        private void ProcessNextOrder()
        {
            if (orders.Count > 0)
            {
                string order = orders.Dequeue();

                if (order.Contains("|"))
                {
                    string[] lines = order.Split(new string[] { "|" }, StringSplitOptions.None);
                    if (lines != null)
                    {
                        Queue<string> neworders = new Queue<string>();

                        if (lines.Count() > 0)
                        {
                            foreach (string line in lines)
                            {
                                neworders.Enqueue(line);
                            }
                        }

                        if (orders.Count > 0)
                        {
                            while (orders.Count != 0)
                            {
                                neworders.Enqueue(orders.Dequeue());
                            }
                        }

                        orders = neworders;
                    }

                    
                }
                else
                {
                    ProcessOrder(order);
                }
            }
        }

        private void ProcessOrder(string order)
        {
            string orderbase = order.Split(new string[] { " " }, StringSplitOptions.None)[0];

            switch (orderbase)
            {
                case "goto":
                    string orderremain = order.Substring(orderbase.Length, order.Length - (orderbase.Length));

                    if (Uri.IsWellFormedUriString(orderremain, UriKind.Absolute))
                    {
                        browser.Load(orderremain);
                    }
                    else
                    {
                        Console.WriteLine("URI is not well formed.");
                    }

                    break;
                case "getsrc":
                    Task<JavascriptResponse> task = browser.GetMainFrame().EvaluateScriptAsync(@"document.getElementsByTagName('html')[0].innerHTML");
                    task.Wait();

                    if (task.Result?.Result != null)
                    {
                        Console.WriteLine(task.Result.Result);
                        Console.WriteLine("%srcend%");
                    }

                    break;
                case "js":
                    orderremain = order.Substring(orderbase.Length, order.Length - (orderbase.Length));

                    browser.GetMainFrame().ExecuteJavaScriptAsync(orderremain);

                    break;
                case "wait":
                    orderremain = order.Substring(orderbase.Length, order.Length - (orderbase.Length));

                    int seconds = 0;
                    if (int.TryParse(orderremain, out seconds))
                    {
                        waitinguntil = DateTime.Now.AddSeconds(seconds);
                        Console.WriteLine("waiting started.");
                    }

                    break;
                case "script":
                    string filename = order.Substring(orderbase.Length, order.Length - (orderbase.Length));

                    if (System.IO.File.Exists(filename))
                    {
                        System.IO.StreamReader reader = new System.IO.StreamReader(filename);
                        string text = reader.ReadToEnd();
                        reader.Close();
                        reader.Dispose();

                        string[] lines = text.Split(new string[] { @"\n", "|" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            orders.Enqueue(line);
                        }
                    }
                    break;
                case "quit":
                    this.Close();
                    break;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (waitinguntil != null)
            {
                if (DateTime.Now.CompareTo(waitinguntil) >= 0)
                {
                    waitinguntil = null;
                    Console.WriteLine("waiting finished.");
                }
            }
            else
            {
                if (!processing)
                {
                    processing = true;
                    ProcessNextOrder();
                    processing = false;
                }
            }
        }
    }

}

