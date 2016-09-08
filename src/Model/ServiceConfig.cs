using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FX.Configuration;

namespace Bilgi.Sis.SftpProxy.Model
{
    public class ServiceConfig : AppConfiguration
    {

        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public bool UploadJobEnabled { get; set; }
        public bool DownloadJobEnabled { get; set; }

    }
}
