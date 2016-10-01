using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Bilgi.Sis.SftpProxy.Model;
using Common.Logging;
using Quartz;
using WinSCP;

namespace Bilgi.Sis.SftpProxy.Jobs
{
    /// <summary>
    /// Upload files to remote location using WinSCP .NET Assmebly.
    /// See : https://winscp.net/eng/docs/library
    /// </summary>
    [DisallowConcurrentExecution]
    public class UploadJob : IJob
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(UploadJob));
        private FileTransferConfig _config;
        private volatile int _isRunning;
        public void Execute(IJobExecutionContext context)
        {
            if (!LoadConfig(context))
                return;

            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            {
                _log.Info("[UPLOAD] SOMEONE ELSE IS IN! I'm just leaving the upload...");
                return;
            }

            try
            {

                DoExecute();
            }
            finally
            {
                _isRunning = 0;
            }
        }

        private bool LoadConfig(IJobExecutionContext context)
        {
            var configFilePath = context.JobDetail.JobDataMap.GetString("configFilePath");
            if (String.IsNullOrWhiteSpace(configFilePath) || !File.Exists(configFilePath))
            {
                _log.Fatal($"Configuration file not specified or does not exist '{configFilePath}'");
                return false;
            }

            _config = FileTransferConfig.LoadFromJsonConfig(configFilePath);

            if (_config == null)
            {
                _log.Fatal($"Configuration file can not be loaded '{configFilePath}'");
                return false;
            }

            return true;
        }

        private void DoExecute()
        {
            foreach (var path in _config.Paths)
            {
                Upload(path);
            }
        }


        private void Upload(PathConfig path)
        {
            _log.Info($"START upload from {path.LocalPath} to {path.RemotePath}");
            UploadTo(path);
            _log.Info($"END upload from {path.LocalPath} to {path.RemotePath}");
        }

        private void UploadTo(PathConfig path)
        {
            var localFileCnt = Directory.GetFiles(path.LocalFolder, path.LocalMask).Count();
            if (localFileCnt == 0)
                return;

            if (!Directory.Exists(path.LocalFolder))
                return;

            // Setup session options
            SessionOptions sessionOptions = _config.Options;

            using (Session session = new Session())
            {
                // Connect
                session.Open(sessionOptions);

                // Download files
                TransferOptions transferOptions = new TransferOptions
                {
                    TransferMode = path.TransferMode,
                    PreserveTimestamp = path.PreserveTimestamp
                };

                TransferOperationResult transferResult = null;

                try
                {
                    bool backupSourceFiles = !String.IsNullOrWhiteSpace(path.BackupSourceFilesFolder) &&
                                             path.BackupSourceFiles;

                    bool deleteSourceFiles = !backupSourceFiles && path.DeleteSourceFiles;
                    var backupFolder = Path.Combine(path.LocalFolder, path.BackupSourceFilesFolder);

                    if (backupSourceFiles && !Directory.Exists(backupFolder))
                        Directory.CreateDirectory(backupFolder);

                    transferResult = session.PutFiles(path.LocalPath, path.RemoteFolder, deleteSourceFiles, transferOptions);
                    transferResult.Check();

                    foreach (TransferEventArgs transfer in transferResult.Transfers)
                    {
                        _log.Info($"Upload SUCCESS '{transfer.FileName}'");
                        FileInfo fi = new FileInfo(transfer.FileName);
                        if (backupSourceFiles)
                        {
                            File.Copy(transfer.FileName,
                                Path.Combine(backupFolder, fi.Name));

                            if (path.DeleteSourceFiles)
                                File.Delete(transfer.FileName);
                        }

                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Can not upload files to remote", ex);
                }

            }
        }
    }
}
