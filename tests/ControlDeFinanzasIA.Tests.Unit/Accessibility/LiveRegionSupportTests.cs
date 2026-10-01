using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// F-021 S8 (R-17, S-36, Reconciliation #8): Avalonia 11.2.7 exposes <c>AutomationProperties.LiveSetting</c>, so the
/// status, error and AI-fallback text blocks set it to <c>Polite</c>. Whether the Windows peer raises the live-region
/// event is NOT verifiable headlessly, and <c>docs/accessibility.md</c> must say so.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class LiveRegionSupportTests
{
    /// <summary>Binding paths that carry a status, an error or an AI fallback caption (not content).</summary>
    private static readonly string[] _statusPaths =
    [
        "FormError", "StatusMessage", "LoadError", "ScheduleError", "GenerationError", "AiStatusMessage",
        "AiTestConnectionMessage", "ExportStatusMessage", "ForecastStateMessage", "AnomalyNarrationStatusText",
        "InsightsStatusText", "PlanNarrationStatusText", "WorkingMessage", "DoneMessage",
    ];

    [Fact]
    public void Avalonia_11_2_7_ExposesTheLiveSettingProperty() => Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(new TextBlock { [AutomationProperties.LiveSettingProperty] = AutomationLiveSetting.Polite }));

    [Fact]
    public void EveryStatusAndErrorTextBlock_InTheViews_IsAPoliteLiveRegion()
    {
        var misses = new List<string>();
        int found = 0;
        foreach (string relative in HardcodedStringsScanTests.AllAxaml())
        {
            var document = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", relative), LoadOptions.SetLineInfo);
            foreach (XElement block in document.Descendants().Where(e => e.Name.LocalName == "TextBlock"))
            {
                string? text = (string?)block.Attribute("Text");
                Match match = Regex.Match(text ?? string.Empty, @"^\{Binding (?<path>\w+)[,}]");
                if (!match.Success || !_statusPaths.Contains(match.Groups["path"].Value, StringComparer.Ordinal))
                {
                    continue;
                }

                found++;
                string? live = (string?)block.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.LiveSetting");
                if (live != "Polite")
                {
                    misses.Add($"{relative}:{((IXmlLineInfo)block).LineNumber} Text={text}");
                }
            }
        }

        Assert.True(found >= 25, $"Only {found} status text blocks found; the scan is not looking at the views.");
        Assert.True(misses.Count == 0, "Status text blocks that are not Polite live regions:" + Environment.NewLine + string.Join(Environment.NewLine, misses));
    }

    [Fact]
    public async Task TheRealExpensesView_AndTheErrorOverlay_RenderTheirStatusTextAsPoliteLiveRegions()
    {
        (AutomationLiveSetting Form, AutomationLiveSetting Status, AutomationLiveSetting OverlayBody, AutomationLiveSetting Copied) result =
            await HeadlessUi.OnUiThread(async () =>
            {
                await using RealShellRig rig = await RealShellRig.CreateAsync("en");
                await rig.OpenSectionAsync(1);
                var expenses = (ExpensesViewModel)rig.Shell.Current!;
                expenses.FormError = "form-error-marker";
                expenses.StatusMessage = "status-marker";
                HeadlessUi.Flush();

                TextBlock Find(string text) => rig.Window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);

                rig.Shell.ErrorOverlay!.Present(Guid.NewGuid(), DateTime.UtcNow);
                rig.Shell.ErrorOverlay.IsCopied = true;
                HeadlessUi.Flush();

                return (
                    AutomationProperties.GetLiveSetting(Find("form-error-marker")),
                    AutomationProperties.GetLiveSetting(Find("status-marker")),
                    AutomationProperties.GetLiveSetting(Find(Strings.ErrorOverlayBody)),
                    AutomationProperties.GetLiveSetting(Find(Strings.ErrorOverlayCopied)));
            });

        Assert.Equal(AutomationLiveSetting.Polite, result.Form);
        Assert.Equal(AutomationLiveSetting.Polite, result.Status);
        Assert.Equal(AutomationLiveSetting.Polite, result.OverlayBody);
        Assert.Equal(AutomationLiveSetting.Polite, result.Copied);
    }

    [Fact]
    public void TheAccessibilityStatement_SaysTheLiveSettingIsSet_ButPeerDeliveryIsUnverified()
    {
        string path = RepoRoot.Combine("docs", "accessibility.md");
        Assert.True(File.Exists(path), "docs/accessibility.md is missing.");
        string text = File.ReadAllText(path);

        Assert.Matches(@"AutomationProperties\.LiveSetting[^.]*\bset\b[^.]*\bPolite\b", text);
        Assert.Matches(@"(?is)live[- ]region.{0,300}unverified.{0,300}Narrator|Narrator.{0,300}unverified", text);
    }
}
