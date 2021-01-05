using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Net;
using System;

namespace ConsoleApplication1
{
    class WMIInfo
    {
        public string SensorType;
        public string Name;
        public string Value;
    }
    
    class Program
    {
        public static HttpListener listener;
        public static string url = "http://192.168.178.21:8080/";

        static void Main(string[] args)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;

                if (request.RawUrl.Contains("stop"))
                {
                    break;
                }

                HttpListenerResponse response = context.Response;

                byte[] buffer = Encoding.UTF8.GetBytes(exposeCpuTempAndLoad());

                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                try
                {
                    output.Write(buffer, 0, buffer.Length);
                }
                catch (Exception _) {}
                finally
                {
                    output.Close();
                }
            }
            listener.Stop(); 
        }

        class CPUInfo
        {
            public string name;
            public string temperature;
            public string load;

            public override string ToString()
            {
                return name + ", " + temperature + ", " + load;
            }
        }

        static string exposeCpuTempAndLoad()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\OpenHardwareMonitor", "SELECT * FROM Sensor");

            List<ManagementObject> objs = searcher.Get().Cast<ManagementObject>().ToList();

            var dict = new Dictionary<string, CPUInfo>();

            objs = objs.FindAll(o =>
            {
                List<PropertyData> props = o.Properties.Cast<PropertyData>().ToList();
                return props.Any(prop => prop.Value.ToString().Contains("CPU Core #")) && (props.Any(prop => prop.Value.ToString().Contains("Temperature")
                    || prop.Value.ToString().Contains("Load")));
            });

            List<WMIInfo> wmis = new List<WMIInfo>();
            objs.ForEach(o =>
            {
                WMIInfo wmi = new WMIInfo();

                List<PropertyData> props = o.Properties.Cast<PropertyData>().ToList();

                props.ForEach(prop =>
                {
                    string val = prop.Value.ToString();
                    switch (prop.Name)
                    {
                        case "Name":
                            wmi.Name = val;
                            break;
                        case "SensorType":
                            wmi.SensorType = val;
                            break;
                        case "Value":
                            wmi.Value = val;
                            break;
                    }
                });
                wmis.Add(wmi);
            });

            wmis.ForEach(wmi =>
            {
                if (!dict.ContainsKey(wmi.Name))
                {
                    CPUInfo cpuInfo = new CPUInfo();
                    cpuInfo.name = wmi.Name;
                    dict.Add(wmi.Name, cpuInfo);
                }

                if (wmi.SensorType == "Temperature")
                {
                    dict[wmi.Name].temperature = wmi.Value;
                }

                if (wmi.SensorType == "Load")
                {
                    dict[wmi.Name].load = wmi.Value;
                }
            });

            List<CPUInfo> vals = dict.Values.ToList();
            vals.Sort((a, b) => a.name.CompareTo(b.name));
            string ret = string.Join("\n", vals.Select(v =>
            {
                try {
                    double truncatedLoad = 0;
                    string load;
                    truncatedLoad = Math.Round(float.Parse(v.load));
                    if (truncatedLoad < 10)
                        load = "0" + truncatedLoad.ToString();
                    else
                        load = truncatedLoad.ToString();
                    return v.temperature + "C " + load + "%";
                } catch (FormatException e) {
                    Console.Out.WriteLine(e.GetBaseException().Message);
                    return v.temperature + "C";
                }
            }));
            Console.Out.WriteLine(ret);
            return ret;
        }
    }
}
