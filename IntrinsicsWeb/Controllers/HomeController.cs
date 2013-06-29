using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IntrinsicsWeb.Controllers
{
    public class CompressResultAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;

            var acceptEncoding = request.Headers["Accept-Encoding"];

            if (string.IsNullOrEmpty(acceptEncoding))
            {
                return;
            }

            acceptEncoding = acceptEncoding.ToUpperInvariant();

            var response = filterContext.HttpContext.Response;

            if (acceptEncoding.Contains("GZIP"))
            {
                response.AppendHeader("Content-encoding", "gzip");
                response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
            }
            else if (acceptEncoding.Contains("DEFLATE"))
            {
                response.AppendHeader("Content-encoding", "deflate");
                response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
            }
        }
    }

    public class MnemonicFamily
    {
        public string Base { get; set; }
        public string Parameters { get; set; }
        public string Latency { get; set; }
        public string Throughput { get; set; }
    }

    public class Parameter
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public class Intrinsic
    {
        public string Name { get; set; }
        public string Tech { get; set; }
        public string ReturnType { get; set; }
        public string CpuId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public string Header { get; set; }
        public string Instruction { get; set; }
        public List<Parameter> Parameters { get; set; }
        public bool IsFloat { get; set; }
        public bool IsInt { get; set; }

        public List<MnemonicFamily> LatencyAndThroughput { get; set; }
    }

    public static class XmlExtensions
    {
        public static string StringValueOrNull(this XElement element)
        {
            return element == null ? null : element.Value.Trim();
        }

        public static string StringValueOrNull(this XAttribute element)
        {
            return element == null ? null : element.Value.Trim();
        }
    }

    public class HomeController : Controller
    {
        private static string GetXmlPath(string name)
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(currentPath, "Xmls", name);
        }

        private static Dictionary<string, List<MnemonicFamily>> ParseLatencyAndThroughput()
        {
            var doc = XDocument.Load(GetXmlPath("LatencyThroughput.xml"));
            if (doc.Root == null)
            {
                throw new HttpException(500, "Invalid xml file");
            }
            var elements = doc.Root.Elements("mnemonicLT");
            var result = new Dictionary<string, List<MnemonicFamily>>();
            foreach (var src in elements)
            {
                string name = src.Attribute("base").Value;
                List<MnemonicFamily> list;
                bool existed = true;
                if (!result.TryGetValue(name, out list))
                {
                    existed = false;
                    list = new List<MnemonicFamily>();
                }

                list.AddRange(src.Elements("family").Select(item => new MnemonicFamily
                    {
                        Base = item.Attribute("base").Value,
                        Latency = item.Attribute("latency").Value,
                        Parameters = item.Attribute("parameters").Value,
                        Throughput = item.Attribute("throughput").Value
                    }));

                if (!existed)
                {
                    result.Add(name, list);
                }
            }
            return result;
        }

        private static void ParseIntrinsicsXml(string name, List<Intrinsic> list)
        {
            var doc = XDocument.Load(GetXmlPath(name));
            if (doc.Root == null)
            {
                throw new HttpException(500, "Invalid xml file");
            }
            var elements = doc.Root.Elements("intrinsic");
            foreach (var src in elements)
            {
                var mnemonics = src.Element("mnemonic");

                var intrinsic = new Intrinsic
                    {
                        Name = src.Attribute("name").Value,
                        Tech = src.Attribute("tech").Value,
                        ReturnType = src.Attribute("rettype").Value,
                        CpuId = src.Element("CPUID").StringValueOrNull(),
                        Category = src.Element("category").StringValueOrNull(),
                        Code = src.Elements("description").FirstOrDefault(f => f.Attribute("code").StringValueOrNull() == "true").StringValueOrNull(),
                        Description = src.Elements("description").FirstOrDefault(f => !f.HasAttributes).StringValueOrNull(),
                        Instruction = mnemonics == null ? null : mnemonics.Attribute("base").Value,
                        Header = src.Element("header").StringValueOrNull(),
                        Parameters = src.Elements("parameter")
                                        .Select(p => new Parameter {Name = p.Attribute("varname").Value, Type = p.Attribute("type").Value})
                                        .ToList()
                    };

                intrinsic.IsInt = intrinsic.Name.Contains("epi") || intrinsic.Name.Contains("epu") || intrinsic.Name.Contains("_pi") ||
                                  intrinsic.Name.Contains("_pu") || (intrinsic.Name.Contains("_si") && !intrinsic.Name.Contains("_sin")) ||
                                  intrinsic.Name.Contains("u8") || intrinsic.Name.Contains("u16") || intrinsic.Name.Contains("u32") ||
                                  intrinsic.Name.Contains("u64") || intrinsic.Name.Contains("si64");

                intrinsic.IsFloat = (intrinsic.Name.Contains("_pd") && (!intrinsic.Name.Contains("_pdep_"))) || intrinsic.Name.Contains("_ps") ||
                                    intrinsic.Name.Contains("_sd") || intrinsic.Name.Contains("_ss") || intrinsic.Name.Contains("pd_") ||
                                    intrinsic.Name.Contains("ps_") || intrinsic.Name.Contains("sd_") ||
                                    (intrinsic.Name.Contains("ss_") && (!intrinsic.Name.Contains("compress_"))) || intrinsic.Name.Contains("f32") ||
                                    intrinsic.Name.Contains("f64");
                list.Add(intrinsic);
            }
        }

#if !DEBUG
        [OutputCache(Duration=60*60*24, Location= System.Web.UI.OutputCacheLocation.ServerAndClient)]
#endif
        [CompressResultAttribute]
        public ActionResult Index()
        {
            var intrinsics = new List<Intrinsic>();
            ParseIntrinsicsXml("MMX.xml", intrinsics);
            ParseIntrinsicsXml("SSE.xml", intrinsics);
            ParseIntrinsicsXml("SSE2.xml", intrinsics);
            ParseIntrinsicsXml("SSE3.xml", intrinsics);
            ParseIntrinsicsXml("SSSE3.xml", intrinsics);
            ParseIntrinsicsXml("SSE4.xml", intrinsics);
            ParseIntrinsicsXml("SSE4.2.xml", intrinsics);
            ParseIntrinsicsXml("AVX.xml", intrinsics);
            ParseIntrinsicsXml("AVX2.xml", intrinsics);
            ParseIntrinsicsXml("FMA.xml", intrinsics);
            ParseIntrinsicsXml("Other.xml", intrinsics);

            var latencies = ParseLatencyAndThroughput();
            foreach (var intrinsic in intrinsics.Where(i => i.Instruction != null))
            {
                List<MnemonicFamily> buffer;
                if (latencies.TryGetValue(intrinsic.Instruction.ToUpperInvariant(), out buffer))
                {
                    intrinsic.LatencyAndThroughput = buffer;
                }
            }

            var model = JsonConvert.SerializeObject(intrinsics, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
            return View((object)model);
        }

    }
}
