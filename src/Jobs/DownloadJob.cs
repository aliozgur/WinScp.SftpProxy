// Author: Ali Özgür (https://github.com/aliozgur) 
// Contact: ali.ozgur@bilgi.edu.tr OR aliozgur79@gmail.com
// 
// Copyright (c) Ali Özgür

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
    /// Download files from remote location using WinSCP .NET Assmebly.
    /// See : https://winscp.net/eng/docs/library
    /// </summary>
    [DisallowConcurrentExecution]
    public class DownloadJob : IJob
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(DownloadJob));
        private FileTransferConfig _config;
        private string _historyFolderName = "hist";
        private volatile int _isRunning;

        public void Execute(IJobExecutionContext context)
        {
            if (!LoadConfig(context))
                return;

            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            {
                _log.Info("[DOWNLOAD] SOMEONE ELSE IS IN! I'm just leaving the upload...");
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
            EnsureDirectories();
            foreach (var path in _config.Paths)
            {
                Download(path);
            }
        }

        private void EnsureDirectories()
        {
            if (_config.Paths.Count == 0)
                return;

            foreach (var path in _config.Paths)
            {
                if (!Directory.Exists(path.LocalFolder))
                    Directory.CreateDirectory(path.LocalFolder);

                if (path.HistoryEnabled)
                {
                    var histDir = Path.Combine(path.LocalFolder, _historyFolderName);
                    if (!Directory.Exists(histDir))
                    {
                        var di = Directory.CreateDirectory(histDir);
                        di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                    }
                }
            }
        }

        private void Download(PathConfig path)
        {
            _log.Info($"START download from {path.RemotePath} to {path.LocalPath}");
            DownloadWithHistory(path);
            DownloadWithoutHistory(path);
            _log.Info($"END download from {path.RemotePath} to {path.LocalPath}");
        }

        private void DownloadWithHistory(PathConfig path)
        {
            if (!path.HistoryEnabled)
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



                var histFolder = Path.Combine(path.LocalFolder, _historyFolderName);
                var histDi = new DirectoryInfo(histFolder);

                var histFileNames = histDi.GetFiles().Select(fi => fi.Name);

                var rf = session.EnumerateRemoteFiles(path.RemoteFolder, path.RemoteMask,
                    EnumerationOptions.None);
                List<RemoteFileInfo> remoteFiles =
                    rf.Where(rfi => !histFileNames.Contains(rfi.Name)).Select(rfi => rfi).ToList();
                foreach (var rfi in remoteFiles)
                {
                    try
                    {
                        transferResult = session.GetFiles(rfi.FullName, path.LocalFolder, path.DeleteSourceFiles,
                            transferOptions);
                        transferResult.Check();
                        File.WriteAllText(Path.Combine(histFolder, rfi.Name), "");
                        _log.Info($"Download SUCCESS '{rfi.FullName}'");

                        MoveRemoteFile(session, rfi.FullName, path);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Can not download '{rfi.FullName}'", ex);
                    }
                }
            }
        }

        private void DownloadWithoutHistory(PathConfig path)
        {
            if (path.HistoryEnabled)
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
                    transferResult = session.GetFiles(path.RemotePath, path.LocalPath, path.DeleteSourceFiles,
                        transferOptions);
                    transferResult.Check();
                    foreach (TransferEventArgs transfer in transferResult.Transfers)
                    {
                        _log.Info($"Download SUCCESS '{transfer.FileName}'");
                        MoveRemoteFile(session, transfer.FileName, path);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Can not download files from remote", ex);
                }

            }
        }

        private void MoveRemoteFile(Session session, string sourcePath, PathConfig path)
        {
            if (!path.MoveRemoteFiles || String.IsNullOrWhiteSpace(path.MoveRemoteFilesTo))
                return;
            try
            {
                var targetPath = path.MoveRemoteFilesTo;
                targetPath = targetPath.EndsWith("/") ? targetPath : targetPath + "/";
                session.MoveFile(sourcePath, targetPath);
                _log.Info($"MOVED remote file {sourcePath} to {path.MoveRemoteFilesTo}");

            }
            catch (Exception ex)
            {
                _log.Error($"Can not move remote file {sourcePath} to {path.MoveRemoteFilesTo}", ex);
            }
        }
    }
}
