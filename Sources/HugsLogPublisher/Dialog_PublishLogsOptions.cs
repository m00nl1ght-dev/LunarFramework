using System;
using UnityEngine;
using Verse;

namespace HugsLogPublisher;

internal class Dialog_PublishLogsOptions : Window
{
    private const float ToggleVerticalSpacing = 4f;
    private readonly ILogPublisherOptions _options;
    private readonly string _text;

    private readonly string _title;

    public Dialog_PublishLogsOptions(string title, string text, ILogPublisherOptions options)
    {
        _title = title;
        _text = text;
        _options = options;
        forcePause = true;
        absorbInputAroundWindow = true;
        forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        doCloseX = true;
        draggable = true;
    }

    public Action OnUpload { get; set; }
    public Action OnCopy { get; set; }
    public Action OnOptionsToggled { get; set; }
    public Action OnPostClose { get; set; }

    public override Vector2 InitialSize => new(550f, 320f);

    public override void PostOpen()
    {
        base.PostOpen();
        UpdateWindowSize();
    }

    public override void PostClose()
    {
        base.PostClose();
        OnPostClose?.Invoke();
    }

    public override void OnAcceptKeyPressed()
    {
        Close();
        OnUpload?.Invoke();
    }

    public override void DoWindowContents(Rect inRect)
    {
        var l = new Listing_Standard { ColumnWidth = inRect.width };
        l.Begin(inRect);
        Text.Font = GameFont.Medium;
        l.Label(_title);
        l.Gap();
        Text.Font = GameFont.Small;
        l.Label(_text);

        const float gapSize = 12f;

        l.Gap(gapSize * 2f);

        _options.UseCustomOptions = !AddOptionCheckbox(l, "HugsLib_logs_useRecommendedSettings", null,
            !_options.UseCustomOptions, out bool optionsUsageChanged);
        if (optionsUsageChanged)
        {
            UpdateWindowSize();
            OnOptionsToggled?.Invoke();
        }

        if (_options.UseCustomOptions)
        {
            const float indent = gapSize * 2f;
            _options.UseUrlShortener = AddOptionCheckbox(l, "HugsLib_logs_shortUrls", "HugsLib_logs_shortUrls_tip",
                _options.UseUrlShortener, out _, indent);
            _options.IncludePlatformInfo = AddOptionCheckbox(l, "HugsLib_logs_platformInfo",
                "HugsLib_logs_platformInfo_tip",
                _options.IncludePlatformInfo, out _, indent);
            _options.AllowUnlimitedLogSize = AddOptionCheckbox(l, "HugsLib_logs_unlimitedLogSize",
                "HugsLib_logs_unlimitedLogSize_tip",
                _options.AllowUnlimitedLogSize, out _, indent);
        }

        l.End();

        var buttonSize = new Vector2((inRect.width - gapSize * 3f) / 3f, 40f);
        var buttonsRect = inRect.BottomPartPixels(buttonSize.y);
        var closeBtnRect = buttonsRect.LeftPartPixels(buttonSize.x);
        if (Widgets.ButtonText(closeBtnRect, "Close".Translate()))
        {
            Close();
        }

        var rightButtonsRect = buttonsRect.RightPartPixels(buttonSize.x * 2f + gapSize);
        if (_options.UseCustomOptions)
        {
            if (Widgets.ButtonText(rightButtonsRect.LeftPartPixels(buttonSize.x),
                    "HugsLib_logs_toClipboardBtn".Translate()))
            {
                Close();
                OnCopy?.Invoke();
            }
        }

        if (Widgets.ButtonText(rightButtonsRect.RightPartPixels(buttonSize.x), "HugsLib_logs_uploadBtn".Translate()))
        {
            OnAcceptKeyPressed();
        }
    }

    private bool AddOptionCheckbox(Listing_Standard listing, string labelKey, string tooltipKey, bool value,
        out bool changed, float indent = 0f)
    {
        bool valueAfter = value;
        var checkRect = listing.GetRect(Text.LineHeight).LeftHalf();
        listing.Gap(ToggleVerticalSpacing);
        checkRect.x += indent;
        if (tooltipKey != null && Mouse.IsOver(checkRect))
        {
            Widgets.DrawHighlight(checkRect);
            TooltipHandler.TipRegion(checkRect, tooltipKey.Translate());
        }

        Widgets.CheckboxLabeled(checkRect, labelKey.Translate(), ref valueAfter);
        changed = valueAfter != value;
        return valueAfter;
    }

    private void UpdateWindowSize()
    {
        const int numHiddenOptions = 3;
        float extraWindowHeight =
            _options.UseCustomOptions ? (Text.LineHeight + ToggleVerticalSpacing) * numHiddenOptions : 0;
        windowRect = new Rect(windowRect.x, windowRect.y, InitialSize.x, InitialSize.y + extraWindowHeight);
    }
}