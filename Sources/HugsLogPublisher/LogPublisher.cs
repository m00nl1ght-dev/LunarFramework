using System;
using System.IO;
using System.Net;
using System.Text;
using LunarFramework;
using RimWorld;
using Verse;

namespace HugsLogPublisher;

/// <summary>
/// Collects the game logs and loaded mods and posts the information on GitHub as a gist.
/// </summary>
public class LogPublisher
{
    public static readonly LogPublisher Instance = new();

    public enum PublisherStatus
    {
        Ready,
        Uploading,
        Done,
        Error
    }

    private const string GatewayPdn = "5894";
    private const string GatewaySdn = "m00nl1ght";
    private const string GatewayEnp = "rwlog";

    private const string RequestUserAgent = "RimWorldLogPublisher";

    private int _requestNum;

    private LogPublisherOptions _publishOptions = new();

    public PublisherStatus Status { get; private set; }
    public string ErrorMessage { get; private set; }
    public string ResultUrl { get; private set; }

    private WebClient _webClient;

    private LogPublisher() {}

    public void ShowPublishPrompt()
    {
        if (PublisherIsReady())
        {
            Find.WindowStack.Add(new Dialog_PublishLogsOptions(
                "HugsLogPublisher.shareConfirmTitle".Translate(),
                "HugsLogPublisher.shareConfirmMessage".Translate(),
                _publishOptions
            )
            {
                OnUpload = OnPublishConfirmed,
                OnCopy = CopyToClipboard
            });
        }
        else
        {
            ShowPublishDialog();
        }
    }

    public void AbortUpload()
    {
        if (Status != PublisherStatus.Uploading) return;

        if (_webClient is { IsBusy: true })
        {
            _webClient.CancelAsync();
            _webClient.Dispose();
            _webClient = null;
        }

        ErrorMessage = "Aborted by user";
        FinalizeUpload(false);
    }

    public void BeginUpload()
    {
        if (!PublisherIsReady()) return;

        Status = PublisherStatus.Uploading;
        ErrorMessage = null;

        var data = LogDataGatherer.PrepareLogData(_publishOptions);

        if (data == null)
        {
            ErrorMessage = "Failed to collect data";
            FinalizeUpload(false);
            return;
        }

        try
        {
            _webClient = new WebClient();
            _webClient.Encoding = Encoding.UTF8;
            _webClient.Headers.Set("User-Agent", RequestUserAgent);
            _webClient.Headers.Set("RW-Version", VersionControl.CurrentVersion.ToString());
            _webClient.Headers.Set("LF-Version", typeof(LunarAPI).Assembly.GetName().Version.ToString());
            _webClient.Headers.Set("UL-Version", typeof(LogPublisher).Assembly.GetName().Version.ToString());
            _webClient.Headers.Set("Request-Idx", $"{_requestNum++}");
            _webClient.Headers.Set("Nonce", $"{163774 * 2334:X}");
            _webClient.UploadStringCompleted += WebClientCallback;
            _webClient.UploadStringAsync(new Uri($"https://{GatewaySdn}.dev:{GatewayPdn}/{GatewayEnp}"), data);
        }
        catch (Exception e)
        {
            OnUploadError(e.Message);
        }
    }

    private void WebClientCallback(object sender, UploadStringCompletedEventArgs result)
    {
        if (result.Cancelled) return;

        if (result.Error == null)
        {
            LogPublisherEntrypoint.LunarAPI.LifecycleHooks.DoOnce(() => OnUploadComplete(result.Result));
            return;
        }

        var message = result.Error.Message;

        if (result.Error is WebException exc)
        {
            using var stream = exc.Response?.GetResponseStream();

            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                message = reader.ReadToEnd();
            }
            else
            {
                message = exc.Status switch
                {
                    WebExceptionStatus.ConnectFailure => "Could not connect to server",
                    WebExceptionStatus.NameResolutionFailure => "Could not connect to server",
                    WebExceptionStatus.Timeout => "Request timed out",
                    _ => exc.Status.ToString()
                };
            }
        }

        LogPublisherEntrypoint.LunarAPI.LifecycleHooks.DoOnce(() => OnUploadError(message));
    }

    public void CopyToClipboard()
    {
        HugsLibUtility.CopyToClipboard(LogDataGatherer.PrepareLogData(_publishOptions));
    }

    private void OnPublishConfirmed()
    {
        if (!_publishOptions.UseCustomOptions) _publishOptions = new LogPublisherOptions();

        BeginUpload();
        ShowPublishDialog();
    }

    private void ShowPublishDialog()
    {
        Find.WindowStack.Add(new Dialog_PublishLogs(this));
    }

    private void OnUploadError(string message)
    {
        ErrorMessage = message;
        LogPublisherEntrypoint.Logger.Warn("Error during log upload: " + message);
        FinalizeUpload(false);
    }

    private void OnUploadComplete(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            OnUploadError("Failed to parse response");
            return;
        }

        ResultUrl = response;
        FinalizeUpload(true);
    }

    private void FinalizeUpload(bool success)
    {
        Status = success ? PublisherStatus.Done : PublisherStatus.Error;

        _webClient?.Dispose();
        _webClient = null;
    }

    private bool PublisherIsReady()
    {
        return Status is PublisherStatus.Ready or PublisherStatus.Done or PublisherStatus.Error;
    }
}
