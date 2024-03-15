using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HugsLogPublisher;

/// <summary>
/// The front-end for LogPublisher.
/// Shows the status of the upload operation, provides controls and shows the produced URL.
/// </summary>
[StaticConstructorOnStartup]
internal class Dialog_PublishLogs : Window
{
    private const float StatusLabelHeight = 60f;
    private const int MaxResultUrlLength = 32;

    private static readonly Texture2D UrlBackgroundTex =
        SolidColorMaterials.NewSolidColorTexture(new Color(0.25f, 0.25f, 0.17f, 0.85f));

    private readonly Vector2 _controlButtonSize = new(150f, 40f);
    private readonly Vector2 _copyButtonSize = new(100f, 40f);

    private readonly LogPublisher _publisher;

    private readonly Dictionary<LogPublisher.PublisherStatus, StatusLabelEntry> _statusMessages = new()
    {
        { LogPublisher.PublisherStatus.Ready, new StatusLabelEntry("", false) },
        { LogPublisher.PublisherStatus.Uploading, new StatusLabelEntry("HugsLogPublisher.uploading", true) },
        { LogPublisher.PublisherStatus.Done, new StatusLabelEntry("HugsLogPublisher.uploaded", false) },
        { LogPublisher.PublisherStatus.Error, new StatusLabelEntry("HugsLogPublisher.uploadError", false) }
    };

    public Dialog_PublishLogs(LogPublisher logPublisher)
    {
        closeOnCancel = true;
        closeOnAccept = false;
        doCloseButton = false;
        doCloseX = true;
        forcePause = true;
        onlyOneOfTypeAllowed = true;
        focusWhenOpened = true;
        draggable = true;
        _publisher = logPublisher;
    }

    public override Vector2 InitialSize => new(500, 250);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        var titleRect = new Rect(inRect.x, inRect.y, inRect.width, 40);
        Widgets.Label(titleRect, "HugsLogPublisher.publisherTitle".Translate());
        Text.Font = GameFont.Small;

        var labelEntry = _statusMessages[_publisher.Status];
        var statusLabelText = labelEntry.RequiresEllipsis
            ? labelEntry.LabelKey.Translate(GenText.MarchingEllipsis(Time.realtimeSinceStartup))
            : labelEntry.LabelKey.Translate();

        if (_publisher.Status == LogPublisher.PublisherStatus.Error)
        {
            statusLabelText = string.Format(statusLabelText, _publisher.ErrorMessage);
        }

        var statusLabelRect = new Rect(inRect.x, inRect.y + titleRect.height, inRect.width, StatusLabelHeight);
        Widgets.Label(statusLabelRect, statusLabelText);
        if (_publisher.Status == LogPublisher.PublisherStatus.Done)
        {
            var urlAreaRect = new Rect(inRect.x, statusLabelRect.y + statusLabelRect.height, inRect.width, _copyButtonSize.y);
            GUI.DrawTexture(urlAreaRect, UrlBackgroundTex);
            var urlLabelRect = new Rect(urlAreaRect.x, urlAreaRect.y, urlAreaRect.width - _copyButtonSize.x, urlAreaRect.height);
            Text.Font = GameFont.Medium;
            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            var croppedResultUrl = _publisher.ResultUrl;
            if (croppedResultUrl.Length > MaxResultUrlLength)
            {
                // crop the url in case shortening has failed and the original url is displayed
                croppedResultUrl = croppedResultUrl.Substring(0, MaxResultUrlLength) + "...";
            }

            Widgets.Label(urlLabelRect, croppedResultUrl);
            Text.Anchor = prevAnchor;
            Text.Font = GameFont.Small;
            var copyBtnRect = new Rect(inRect.width - _copyButtonSize.x, urlAreaRect.y, _copyButtonSize.x, _copyButtonSize.y);

            if (Widgets.ButtonText(copyBtnRect, "HugsLogPublisher.copy".Translate()))
            {
                HugsLibUtility.CopyToClipboard(_publisher.ResultUrl);
            }
        }

        var bottomLeftBtnRect = new Rect(inRect.x, inRect.height - _controlButtonSize.y, _controlButtonSize.x,
            _controlButtonSize.y);
        if (_publisher.Status == LogPublisher.PublisherStatus.Error)
        {
            if (Widgets.ButtonText(bottomLeftBtnRect, "HugsLogPublisher.retryBtn".Translate()))
            {
                _publisher.BeginUpload();
            }
        }
        else if (_publisher.Status == LogPublisher.PublisherStatus.Done)
        {
            if (Widgets.ButtonText(bottomLeftBtnRect, "HugsLogPublisher.browseBtn".Translate()))
            {
                Application.OpenURL(_publisher.ResultUrl);
            }
        }

        var bottomRightBtnRect = new Rect(inRect.width - _controlButtonSize.x, inRect.height - _controlButtonSize.y,
            _controlButtonSize.x, _controlButtonSize.y);
        if (_publisher.Status == LogPublisher.PublisherStatus.Uploading)
        {
            if (Widgets.ButtonText(bottomRightBtnRect, "HugsLogPublisher.abortBtn".Translate()))
            {
                _publisher.AbortUpload();
            }
        }
        else
        {
            if (Widgets.ButtonText(bottomRightBtnRect, "CloseButton".Translate()))
            {
                Close();
            }
        }
    }

    private class StatusLabelEntry
    {
        public readonly string LabelKey;
        public readonly bool RequiresEllipsis;

        public StatusLabelEntry(string labelKey, bool requiresEllipsis)
        {
            LabelKey = labelKey;
            RequiresEllipsis = requiresEllipsis;
        }
    }
}
