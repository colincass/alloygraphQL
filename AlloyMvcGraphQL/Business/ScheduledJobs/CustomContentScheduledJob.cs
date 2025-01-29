using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.ServiceLocation;
using EPiServer.Shell.Modules;
using Microsoft.Extensions.Options;
using Optimizely.ContentGraph.Cms.Configuration;
using Optimizely.ContentGraph.Cms.NetCore.Extensions.Internal;
using Optimizely.ContentGraph.Cms.Services.Internal;
using Optimizely.ContentGraph.Core;
using Optimizely.ContentGraph.Core.Api.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace Optimizely.ContentGraph.Cms.Job.Internal
{


    [ScheduledPlugIn(DisplayName = "Custom Graph job",
        Description = "Specify whenever synchronizing all content",
        IntervalLength = 1,
        IntervalType = EPiServer.DataAbstraction.ScheduledIntervalType.Weeks)]
    [ServiceConfiguration]
    public class CustomContentIndexingJob : ScheduledJobBase, IDisposable
    {
        private readonly IContentIndexingJobService _indexingJobService;
        private readonly IContentTypeIndexingJobService _contentTypeIndexingJobService;
        private readonly ProtectedModuleOptions _protectedModuleOptions;

        private bool _disposed;
        private CancellationTokenSource _cancellationTokenSource;

        public CustomContentIndexingJob()
            : this(
                ServiceLocator.Current.GetInstance<IContentTypeIndexingJobService>(),
                ServiceLocator.Current.GetInstance<IContentIndexingJobService>(),
                ServiceLocator.Current.GetInstance<ProtectedModuleOptions>(),
                ServiceLocator.Current.GetInstance<IClient>(),
                ServiceLocator.Current.GetInstance<IOptions<QueryOptions>>())
        {
        }

        public CustomContentIndexingJob(
            IContentTypeIndexingJobService contentTypeIndexingJobService,
            IContentIndexingJobService indexingJobService,
            ProtectedModuleOptions protectedModuleOptions,
            IClient client,
            IOptions<QueryOptions> queryOptions)
        {
            IsStoppable = true;
            _contentTypeIndexingJobService = contentTypeIndexingJobService;
            _indexingJobService = indexingJobService;
            _protectedModuleOptions = protectedModuleOptions;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Called when a user clicks on Stop for a manually started job, or when ASP.NET shuts down.
        /// </summary>
        public override void Stop()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Called when a scheduled job executes
        /// </summary>
        /// <returns>A status message to be stored in the database log and visible from admin mode</returns>
        public override string Execute()
        {
            try
            {
                //Call OnStatusChanged to periodically notify progress of job for manually started jobs
                OnStatusChanged("Starting execution of ContentTypeIndexingJob");

                var indexingJobId = Guid.NewGuid();
                //_graphSyncContextAccessor.Context = new(indexingJobId: indexingJobId.ToString());

                var contentTypeIndexingResults = _contentTypeIndexingJobService.Start(_cancellationTokenSource.Token);
                if (contentTypeIndexingResults?.Result == Result.Error)
                {
                    //encode the message in case it includes html elements to avoid rendering it on CMS UI.
                    var encodedErrorMessages = HttpUtility.HtmlEncode(contentTypeIndexingResults.Error?.Message);
                    throw new Exception(
                        $"There's an issue during content types are being indexed: {encodedErrorMessages}.");
                }

                OnStatusChanged("Starting execution of ContentIndexingJob");

                var contentIndexingResults =
                    _indexingJobService.Start(_cancellationTokenSource.Token).GetAwaiter().GetResult();

                var indexingTask = _indexingJobService.SendIndexingJobResult(indexingJobId, contentIndexingResults);

                if (contentIndexingResults != null && contentIndexingResults.Any(t => t.Result == Result.Error))
                {
                    //encode the message in case it includes html elements to avoid rendering it on CMS UI.
                    var errorMessages = contentIndexingResults
                        .Where(t => t.Result == Result.Error)
                        .Select(i => HttpUtility.HtmlEncode(i.Error?.Message) ?? "");
                    throw new Exception(
                        $"There's an issue during contents are being indexed: {string.Join(", ", errorMessages)}. (see log for more information)");
                }

                var sentJournal = indexingTask.GetAwaiter().GetResult();

                if (sentJournal == 0)
                {
                    return "Optimizely Graph content synchronization job run completely!";
                }

                var jobDetailsLink =
                    $"<a href='/{_protectedModuleOptions.GetRootPath()}/contentgraph/journal/status?jobId={indexingJobId}' target='_blank'>Details</a>";


                return BuildSuccessMessage(jobDetailsLink, contentIndexingResults);

            }
            catch (OperationCanceledException oce)
            {
                return $"Stop of job was called. Stack: {oce.StackTrace}";
            }
        }

        private string BuildSuccessMessage(string jobDetailsLink, IEnumerable<Response> contentIndexingResults)
        {
            var warningMsgs = contentIndexingResults
                .Where(r => r.Result == Result.Warning)
                .Select(r => r.WarningMessage);
            return $"Optimizely Graph content synchronization job run completely! {jobDetailsLink}<br/>" +
                   $"{string.Join("<br/>", warningMsgs)}";
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                _disposed = true;
            }

        }

    }
}