using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WinSCP;

namespace Bilgi.Sis.SftpProxy.Model
{
    public class PathConfig
    {
        public string RemoteFolder { get; set; }
        public string RemoteMask { get; set; }
        public string LocalFolder { get; set; }
        public string LocalMask { get; set; }

        public TransferMode TransferMode { get; set; } = TransferMode.Binary;
        public bool PreserveTimestamp { get; set; } = true;

        public bool DeleteSourceFiles { get; set; } = false;

        public bool HistoryEnabled { get; set; } = false;

        public bool MoveRemoteFiles { get; set; } = false;

        public string MoveRemoteFilesTo { get; set; }

        public string BackupSourceFilesFolder { get; set; }
        public bool BackupSourceFiles { get; set; }


        [JsonIgnore]
        public string RemotePath
        {
            get
            {
                if (!RemoteFolder.StartsWith("/"))
                    RemoteFolder = "/" + RemoteFolder;
                if (!RemoteFolder.EndsWith("/"))
                    RemoteFolder = RemoteFolder + "/";

                return !String.IsNullOrWhiteSpace(RemoteMask)
                    ? RemoteFolder + RemoteMask
                    : RemoteFolder;

            }
        }

        [JsonIgnore]
        public string LocalPath
        {
            get
            {
                if (!LocalFolder.EndsWith("\\"))
                    LocalFolder = LocalFolder + "\\";

                return !String.IsNullOrWhiteSpace(LocalFolder)
                    ? LocalFolder + LocalMask
                    : LocalFolder;

            }
        }
    }
}
