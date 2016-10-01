using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WinSCP;

namespace Bilgi.Sis.SftpProxy.Model
{
    public class FileTransferConfig
    {
        public SessionOptions Options { get; set; }
        public int IntervalInSeconds { get; set; } = 3600; // 1 hour default
        public string CronExp { get; set; }

        public List<PathConfig> Paths { get; set; }

        public static FileTransferConfig LoadFromJsonConfig(string path)
        {
            string json = File.ReadAllText(path);
            var result = JsonConvert.DeserializeObject<FileTransferConfig>(json);
            return result;
        }

    }
}
