using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bilgi.Sis.SftpProxy.Jobs;
using Bilgi.Sis.SftpProxy.Model;
using Common.Logging;
using Quartz;
using Quartz.Impl;

namespace Bilgi.Sis.SftpProxy
{
    public class Service
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(Service));
        private IScheduler _scheduler = null;
        private FileTransferConfig _downloadConfig;
        private int _downloadInterval = 60 * 60; //  1 hour
        private string _downloadCron;

        private FileTransferConfig _uploadConfig;
        private int _uploadInterval = 60 * 60; //  1 hour
        private string _uploadCron;

        private string DownloadConfigFilePath => Path.Combine(Environment.CurrentDirectory, "configDownload.json");
        private string UploadConfigFilePath => Path.Combine(Environment.CurrentDirectory, "configUpload.json");

        private ServiceConfig _svcConfig = new ServiceConfig();
        private void ReadConfiguration()
        {
            ConfigureDownloads();
            ConfigureUploads();
        }



        private void ConfigureDownloads()
        {
            if (!_svcConfig.DownloadJobEnabled)
                return;

            if (File.Exists(DownloadConfigFilePath))
            {
                _downloadConfig = FileTransferConfig.LoadFromJsonConfig(DownloadConfigFilePath);

                if (_downloadConfig.IntervalInSeconds > 0)
                    _downloadInterval = _downloadConfig.IntervalInSeconds;

                if (!String.IsNullOrWhiteSpace(_downloadConfig.CronExp))
                    _downloadCron = _downloadConfig.CronExp;
            }
            else
            {
                _log.Fatal($"Configuration file does not exist {DownloadConfigFilePath}");
            }
        }

        private void ConfigureUploads()
        {
            if (!_svcConfig.UploadJobEnabled)
                return;


            if (File.Exists(UploadConfigFilePath))
            {
                _uploadConfig = FileTransferConfig.LoadFromJsonConfig(UploadConfigFilePath);

                if (_uploadConfig.IntervalInSeconds > 0)
                    _uploadInterval = _uploadConfig.IntervalInSeconds;
                if (!String.IsNullOrWhiteSpace(_uploadConfig.CronExp))
                    _uploadCron = _uploadConfig.CronExp;
            }
            else
            {
                _log.Fatal($"Configuration file does not exist {UploadConfigFilePath}");
            }
        }

        public void Start()
        {
            try
            {
                ReadConfiguration();
                DoStart();
            }
            catch (SchedulerConfigException ex)
            {
                _log.Fatal("Can not start service, Quartz scheduler configuration error", ex);
            }
            catch (SchedulerException ex)
            {
                _log.Fatal("Can not start service, Quartz scheduler error", ex);
            }
            catch (Exception ex)
            {
                _log.Fatal("Can not start service", ex);
            }
        }

        public void Stop()
        {
            DoStop();
        }

        private void DoStart()
        {
            if (!_svcConfig.UploadJobEnabled && !_svcConfig.DownloadJobEnabled)
            {
                _log.Info("Neither Download nor Upload job enabled");
                return;
            }

            StartScheduler();

            if (_svcConfig.DownloadJobEnabled)
                PrepareDownloadJob();

            if (_svcConfig.UploadJobEnabled)
                PrepareUploadJob();
        }

        private void DoStop()
        {
            if (_scheduler != null && _scheduler.IsStarted)
                _scheduler.Shutdown();
        }

        private void StartScheduler()
        {
            if (_scheduler == null)
                _scheduler = StdSchedulerFactory.GetDefaultScheduler();

            _scheduler.Start();
        }

        private void PrepareDownloadJob()
        {
            if (_downloadConfig == null)
                return;

            var jobKey = new JobKey("DownloadJob", "DownloadsGroup");
            IJobDetail job = JobBuilder.Create<DownloadJob>()
                .WithIdentity(jobKey)
                .UsingJobData("configFilePath", DownloadConfigFilePath)
                .Build();

            ITrigger trigger = null;
            bool explicitTriggerNow = false;

            if (!String.IsNullOrWhiteSpace(_downloadCron))
            {

                trigger = TriggerBuilder.Create()
                    .WithSchedule(
                        CronScheduleBuilder.CronSchedule(_downloadCron)
                        .WithMisfireHandlingInstructionIgnoreMisfires()
                    ).Build();
                explicitTriggerNow = true;
            }
            else
            {
                trigger = TriggerBuilder.Create()
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInSeconds(_downloadInterval)
                        .WithMisfireHandlingInstructionIgnoreMisfires()
                        .RepeatForever())
                    .Build();
            }

            _scheduler.ScheduleJob(job, trigger);
            if (explicitTriggerNow)
                _scheduler.TriggerJob(jobKey);
        }

        private void PrepareUploadJob()
        {
            if (_uploadConfig == null)
                return;

            var jobKey = new JobKey("UploadJob", "UploadsGroup");
            IJobDetail job = JobBuilder.Create<UploadJob>()
                .WithIdentity(jobKey)
                .UsingJobData("configFilePath", UploadConfigFilePath)
                .Build();

            ITrigger trigger = null;
            bool explicitTriggerNow = false;

            if (!String.IsNullOrWhiteSpace(_uploadCron))
            {

                trigger = TriggerBuilder.Create()
                    .WithSchedule(
                        CronScheduleBuilder.CronSchedule(_uploadCron)
                            .WithMisfireHandlingInstructionIgnoreMisfires()
                    ).Build();
                explicitTriggerNow = true;
            }
            else
            {
                trigger = TriggerBuilder.Create()
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInSeconds(_uploadInterval)
                        .WithMisfireHandlingInstructionIgnoreMisfires()
                        .RepeatForever())
                    .Build();
            }

            _scheduler.ScheduleJob(job, trigger);
            if (explicitTriggerNow)
                _scheduler.TriggerJob(jobKey);
        }
    }
}
