using System;
using System.Net;
using System.Text;
using LunarFramework;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
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

    private const float PublishRequestTimeout = 90f;

    private UnityWebRequest _activeRequest;
    private int _requestNum;

    private LogPublisherOptions _publishOptions = new();
    private bool _userAborted;

    public PublisherStatus Status { get; private set; }
    public string ErrorMessage { get; private set; }
    public string ResultUrl { get; private set; }

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
        _userAborted = true;

        if (_activeRequest is { isDone: false })
        {
            _activeRequest.Abort();
        }

        _activeRequest = null;

        ErrorMessage = "Aborted by user";
        FinalizeUpload(false);
    }

    public void BeginUpload()
    {
        if (!PublisherIsReady()) return;

        Status = PublisherStatus.Uploading;
        ErrorMessage = null;
        _userAborted = false;

        var content = LogDataGatherer.PrepareLogData(_publishOptions);

        if (content == null)
        {
            ErrorMessage = "Failed to collect data";
            FinalizeUpload(false);
            return;
        }

        void OnRequestFailed(string message)
        {
            if (_userAborted) return;
            OnRequestError(message);
            Log.Warning("Exception during log publishing: " + message);
        }

        try
        {
            _activeRequest = new UnityWebRequest($"https://{GatewaySdn}.dev:{GatewayPdn}/{GatewayEnp}", UnityWebRequest.kHttpVerbPOST);
            _activeRequest.SetRequestHeader("RW-Version", VersionControl.CurrentVersion.ToString());
            _activeRequest.SetRequestHeader("LF-Version", typeof(LunarAPI).Assembly.GetName().Version.ToString());
            _activeRequest.SetRequestHeader("UL-Version", typeof(LogPublisher).Assembly.GetName().Version.ToString());
            _activeRequest.SetRequestHeader("Request-Idx", $"{_requestNum++}");
            _activeRequest.SetRequestHeader("User-Agent", RequestUserAgent);
            _activeRequest.SetRequestHeader("Nonce", $"{163774*2334:X}");
            _activeRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(content)) { contentType = "text/plain" };
            _activeRequest.downloadHandler = new DownloadHandlerBuffer();
            HugsLibUtility.AwaitUnityWebResponse(_activeRequest, OnUploadComplete, OnRequestFailed, HttpStatusCode.OK, PublishRequestTimeout);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            OnRequestFailed(e.Message);
        }
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

    private void OnRequestError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        FinalizeUpload(false);
    }

    private void OnUploadComplete(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            OnRequestError("Failed to parse response");
            return;
        }

        ResultUrl = response;
        FinalizeUpload(true);
    }

    private void FinalizeUpload(bool success)
    {
        Status = success ? PublisherStatus.Done : PublisherStatus.Error;
        _activeRequest = null;
    }

    private bool PublisherIsReady()
    {
        return Status is PublisherStatus.Ready or PublisherStatus.Done or PublisherStatus.Error;
    }
}
