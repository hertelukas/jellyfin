#pragma warning disable CS1591

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Helpers;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.LiveTv.EmbyTV
{
    public class DirectRecorder : IRecorder
    {
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IStreamHelper _streamHelper;

        public DirectRecorder(ILogger logger, IHttpClientFactory httpClientFactory, IStreamHelper streamHelper)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _streamHelper = streamHelper;
        }

        public string GetOutputPath(MediaSourceInfo mediaSource, string targetFile)
        {
            return targetFile;
        }

        public Task Record(IDirectStreamProvider? directStreamProvider, MediaSourceInfo mediaSource, string targetFile, TimeSpan duration, Action onStarted, CancellationToken cancellationToken)
        {
            if (directStreamProvider != null)
            {
                return RecordFromDirectStreamProvider(directStreamProvider, targetFile, duration, onStarted, cancellationToken);
            }

            return RecordFromMediaSource(mediaSource, targetFile, duration, onStarted, cancellationToken);
        }

        private async Task RecordFromDirectStreamProvider(IDirectStreamProvider directStreamProvider, string targetFile, TimeSpan duration, Action onStarted, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? throw new ArgumentException("Path can't be a root directory.", nameof(targetFile)));

            // use FileShare.None as this bypasses dotnet bug dotnet/runtime#42790 .
            using (var output = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, IODefaults.FileStreamBufferSize, AsyncFile.UseAsyncIO))
            {
                onStarted();

                _logger.LogInformation("Copying recording to file {FilePath}", targetFile);

                // The media source is infinite so we need to handle stopping ourselves
                using var durationToken = new CancellationTokenSource(duration);
                using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationToken.Token);
                var linkedCancellationToken = cancellationTokenSource.Token;

                await using var fileStream = new ProgressiveFileStream(directStreamProvider.GetStream());
                await _streamHelper.CopyToAsync(
                    fileStream,
                    output,
                    IODefaults.CopyToBufferSize,
                    1000,
                    linkedCancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Recording completed: {FilePath}", targetFile);
        }

        private async Task RecordFromMediaSource(MediaSourceInfo mediaSource, string targetFile, TimeSpan duration, Action onStarted, CancellationToken cancellationToken)
        {
            using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                .GetAsync(mediaSource.Path, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Opened recording stream from tuner provider");

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? throw new ArgumentException("Path can't be a root directory.", nameof(targetFile)));

            // use FileShare.None as this bypasses dotnet bug dotnet/runtime#42790 .
            await using var output = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, IODefaults.CopyToBufferSize, AsyncFile.UseAsyncIO);

            onStarted();

            _logger.LogInformation("Copying recording stream to file {0}", targetFile);

            // The media source if infinite so we need to handle stopping ourselves
            using var durationToken = new CancellationTokenSource(duration);
            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationToken.Token);
            cancellationToken = linkedCancellationToken.Token;

            await _streamHelper.CopyUntilCancelled(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                output,
                IODefaults.CopyToBufferSize,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Recording completed to file {0}", targetFile);
        }
    }
}
