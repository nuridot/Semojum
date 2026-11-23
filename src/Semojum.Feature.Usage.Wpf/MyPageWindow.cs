using System.Windows;
using Semojum.Core.Integration;
using Semojum.Feature.Usage.Wpf;

namespace Semojum.Desktop.Dialogs;

public sealed class MyPageWindow : CompactDialog
{
    private readonly SemojumApiClient api;
    private readonly string workInfo;
    private readonly UsageActions actions;
    public string? JobId { get; private set; }
    public string? FileName { get; private set; }
    public bool LogoutRequested { get; private set; }
    public bool CredentialsRevoked { get; private set; }

    public MyPageWindow(SemojumApiClient api, string workInfo, UsageActions actions)
        : base("마이페이지 - 누리닷", 1440, 1000, editor: true)
    {
        this.api = api;
        this.workInfo = workInfo;
        this.actions = actions;
        Body(new MyPageView(this, api, actions, Lifetime.Token));
        Loaded += (_, _) => WindowState = WindowState.Maximized;
    }

    internal void OpenHistory(bool inquiries)
    {
        var detail = new UsageDialog(api, workInfo, actions, openInquiries: inquiries) { Owner = this };
        if (detail.ShowDialog() != true) return;
        if (detail.LogoutRequested) { Logout(detail.CredentialsRevoked); return; }
        if (detail.JobId is null) return;
        JobId = detail.JobId;
        FileName = detail.FileName;
        DialogResult = true;
    }

    internal void Logout(bool revoked = false)
    {
        LogoutRequested = true;
        CredentialsRevoked = revoked;
        DialogResult = true;
    }
}
