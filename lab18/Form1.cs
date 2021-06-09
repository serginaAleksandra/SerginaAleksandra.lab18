using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace lab18
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        interface ISimulationAgent
        {
            void Taou();
        }
        class Cassa : ISimulationAgent
        {
            private readonly FlowLayoutPanel panel;
            private readonly Label label;
            public int N { get; }
            private readonly Random random;
            public bool Busy { get => Posetitel != null; }
            private DateTime serviceFinishTime = DateTime.MaxValue;
            private Customer Posetitel = null;
            public double ServiceLeft
            {
                get
                {
                    var ts = serviceFinishTime - DateTime.Now;
                    return ts.TotalSeconds;
                }
            }
            public Cassa(int n, Random random, FlowLayoutPanel tellerPanel)
            {
                N = n;
                this.random = random;
                panel = tellerPanel;
                label = new Label
                {
                    Text = $"Cassa {n} is free",
                    Width = 125,
                    Height = 75,
                    BorderStyle = BorderStyle.FixedSingle,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };
                panel.Controls.Add(label);
            }

            public event EventHandler<int> TellerFree;
            public void Taou()
            {
                if (DateTime.Now > serviceFinishTime)
                {
                    serviceFinishTime = DateTime.MaxValue;
                    Posetitel = null;
                    TellerFree?.Invoke(this, N);
                    label.Text = $"Cassa {N} is free";
                }
                else if (Posetitel != null) label.Text = $"Cassa {N} is servicing\nPosetiel {Posetitel.N}\n{ServiceLeft,5:F1}s left";
            }

            public void AcceptCustomer(Customer customer)
            {
                this.Posetitel = customer;
                double serviceTime = Util.UniformSample(customer.MinServiceTime, customer.MaxServiceTime, random);
                TimeSpan ts = new TimeSpan(0, 0, 0, (int)serviceTime, (int)(serviceTime * 1000));
                serviceFinishTime = DateTime.Now + ts;
            }
        }
        static class Util
        {
            public static double ExponentialSample(double intensity, Random random)
            {
                return -Math.Log(random.NextDouble()) / intensity;
            }

            public static double GaussianSample(double mean, double var, Random random)
            {
                double result = 0;
                for (int i = 0; i < 12; i++)
                {
                    result += random.NextDouble();
                }
                return Math.Sqrt(var) * (result - 6) + mean;
            }

            public static double UniformSample(double min, double max, Random random)
            {
                return random.NextDouble() * (max - min) + min;
            }

        }
        class SimulationManager
        {
            private readonly List<ISimulationAgent> agents = new List<ISimulationAgent>();
            private readonly List<ISimulationAgent> newAgents = new List<ISimulationAgent>();
            private readonly List<ISimulationAgent> removeAgents = new List<ISimulationAgent>();
            private readonly TextBox log;
            private readonly Random rnd = new Random();
            private readonly DateTime start = DateTime.Now;
            private readonly List<Cassa> Cassas = new List<Cassa>();
            private readonly QueueManager queueManager;
            private readonly int CassaN = 5;

            public double SecondsPassed
            {
                get
                {
                    var t = DateTime.Now - start;
                    return t.TotalSeconds;
                }
            }

            public SimulationManager(FlowLayoutPanel customerPanel, FlowLayoutPanel tellerPanel, TextBox logTextBox)
            {
                log = logTextBox;
                queueManager = new QueueManager(customerPanel);
                var customerGen = new CustomerGenerator(rnd);
                customerGen.SpawnCustomer += HandleSpawnCustomer;
                for (int i = 0; i < CassaN; i++)
                {
                    var t = new Cassa(i + 1, rnd, tellerPanel);
                    t.TellerFree += HandleTellerFree;
                    Cassas.Add(t);
                }
                agents.Add(customerGen);
                agents.Add(queueManager);
                agents.AddRange(Cassas);
            }

            private void HandleSpawnCustomer(object sender, int customerN)
            {
                var Posetitel = new Customer(customerN, rnd);
                newAgents.Add(Posetitel);
                Posetitel.CustomerUnqueues += HandleCustomerUnqueue;
                Cassa t = Cassas.Find(x => !x.Busy);
                if (t != null)
                {
                    log.Text += $"{SecondsPassed,-5:F1}s Posetitel {customerN} has arrived and went to Teller {t.N}" + Environment.NewLine;
                    t.AcceptCustomer(Posetitel);
                }
                else
                {
                    log.Text += $"{SecondsPassed,-5:F1}s Posetitel {customerN} has arrived and queued" + Environment.NewLine;
                    queueManager.QueueCustomer(Posetitel);
                    Posetitel.CustomerUnqueues += queueManager.HandleCustomerUnqueue;
                }
            }

            private void HandleTellerFree(object sender, int n)
            {
                Cassa t = (Cassa)sender;
                Customer next = queueManager.NextInLine;
                if (next != null)
                {
                    log.Text += $"{SecondsPassed,-5:F1}s Cassa {n} has finished and accepted Customer {next.N} from queue" + Environment.NewLine;
                    next.Unqueue();
                    t.AcceptCustomer(next);
                }
                else
                {
                    log.Text += $"{SecondsPassed,-5:F1}s Cassa {n} has finished and is now free" + Environment.NewLine;
                }
            }

            private void HandleCustomerUnqueue(object sender, int customerN)
            {
                log.Text += $"{SecondsPassed,-5:F1}s Pokupatel {customerN} has left the queue" + Environment.NewLine;
                removeAgents.Add((ISimulationAgent)sender);
            }

            public void Taou()
            {
                foreach (var a in agents)
                {
                    a.Taou();
                }
                if (removeAgents.Count > 0)
                {
                    foreach (var a in removeAgents)
                    {
                        agents.Remove(a);
                    }
                    removeAgents.Clear();
                }
                if (newAgents.Count > 0)
                {
                    agents.AddRange(newAgents);
                    newAgents.Clear();
                }
            }
        }
        class QueueManager : ISimulationAgent
        {
            private readonly FlowLayoutPanel panel;
            private readonly Dictionary<Customer, Label> labelDict = new Dictionary<Customer, Label>();
            private readonly List<Customer> customers = new List<Customer>();
            public Customer NextInLine
            {
                get
                {
                    if (customers.Count > 0)
                    {
                        return customers[0];
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            public QueueManager(FlowLayoutPanel flowLayoutPanel)
            {
                panel = flowLayoutPanel;
            }

            public void QueueCustomer(Customer customer)
            {
                if (labelDict.ContainsKey(customer))
                {
                    return;
                }

                customers.Add(customer);
                var label = new Label
                {
                    Text = $"Customer {customer.N}",
                    Height = 50,
                    Width = 75,
                    BorderStyle = BorderStyle.FixedSingle,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };
                labelDict[customer] = label;
                panel.Controls.Add(label);
            }

            public void HandleCustomerUnqueue(object sender, int n)
            {
                Customer c = (Customer)sender;
                if (labelDict.TryGetValue(c, out Label l))
                {
                    customers.Remove(c);
                    labelDict.Remove(c);
                    panel.Controls.Remove(l);
                }
            }

            public void Taou()
            {
                foreach (var kv in labelDict)
                {
                    kv.Value.Text = $"Customer {kv.Key.N} will leave in {kv.Key.WaitingLeft,5:F1}s";
                }
            }
        }
        class Customer : ISimulationAgent
        {
            public int N { get; }
            public double MinServiceTime { get; }
            public double MaxServiceTime { get; }

            private readonly DateTime waitFinishTime;

            public double WaitingLeft
            {
                get
                {
                    var ts = waitFinishTime - DateTime.Now;
                    return ts.TotalSeconds;
                }
            }
            public event EventHandler<int> CustomerUnqueues;

            public Customer(int customerN, Random random)
            {
                N = customerN;
                var waitTime = Util.GaussianSample(7, 1.5, random);
                TimeSpan waitTS = new TimeSpan(0, 0, 0, (int)waitTime, (int)(waitTime * 1000));
                waitFinishTime = DateTime.Now + waitTS;

                var t1 = Util.ExponentialSample(0.1, random);
                var t2 = Util.ExponentialSample(0.1, random);
                if (t1 < t2)
                {
                    MinServiceTime = t1;
                    MaxServiceTime = t2;
                }
                else
                {
                    MinServiceTime = t2;
                    MaxServiceTime = t1;
                }
            }

            public void Taou()
            {
                if (DateTime.Now > waitFinishTime)
                {
                    Unqueue();
                }
            }

            public void Unqueue()
            {
                CustomerUnqueues?.Invoke(this, N);
            }
        }
        class CustomerGenerator : ISimulationAgent
        {
            private static readonly double maxLambda = 3;
            private static double Lambda(double x)
            {
                return Math.Sin(x / 4) + 1;
            }

            private static double Wri(Random random)
            {
                double t = Util.ExponentialSample(maxLambda, random);
                double u = maxLambda * random.NextDouble();
                while (Lambda(t) < u)
                {
                    t += Util.ExponentialSample(maxLambda, random);
                    u = maxLambda * random.NextDouble();
                }
                return t;
            }

            private readonly Random random;
            public event EventHandler<int> SpawnCustomer;

            private DateTime nextCustomer;
            private int customerN = 1;

            public CustomerGenerator(Random random)
            {
                this.random = random;
                UpdateNextCustomer();
            }

            public void Taou()
            {
                if (DateTime.Now >= nextCustomer)
                {
                    SpawnCustomer?.Invoke(this, customerN++);
                    UpdateNextCustomer();
                }
            }

            private void UpdateNextCustomer()
            {
                var t = Wri(random);
                TimeSpan offset = new TimeSpan(0, 0, 0, (int)t, (int)(t * 1000));
                nextCustomer = DateTime.Now + offset;
            }
        }

        private SimulationManager simulationManager;

        private void button1_Click(object sender, EventArgs e)
        {
            if (timer1.Enabled)
            {
                timer1.Stop();
            }
            else
            {
                flowLayoutPanel1.Controls.Clear();
                flowLayoutPanel2.Controls.Clear();
                textBox1.Text = string.Empty;
                simulationManager = new SimulationManager(flowLayoutPanel1, flowLayoutPanel2, textBox1);
                timer1.Start();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            simulationManager.Taou();
            label1.Text = $"{simulationManager.SecondsPassed:F1}s";
        }
    }
}
