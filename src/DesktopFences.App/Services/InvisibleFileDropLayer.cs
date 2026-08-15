using DesktopFences.Native;
using Color = System.Drawing.Color;
using Form = System.Windows.Forms.Form;
using Message = System.Windows.Forms.Message;

namespace DesktopFences.App.Services;

/// <summary>
/// Alvo OLE minúsculo colado no hotspot do cursor, HWND_TOPMOST a cada move.
/// O thumbnail do Explorer é outra janela topmost no ponteiro; WindowFromPoint
/// acerta ela, não a fence. Esta janela é recolocada por cima a cada pixel.
/// LWA_ALPHA uniforme (não per-pixel) para o retângulo inteiro ser hit-test.
/// Alpha baixo demais para o usuário ver; o ghost (click-through) fica acima.
/// </summary>
internal sealed class InvisibleFileDropLayer : Form
{
    private const int HitSize = 36;
    private const byte HitAlpha = 2;

    public InvisibleFileDropLayer()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AllowDrop = false;
        BackColor = Color.FromArgb(12, 12, 18);
        Size = new System.Drawing.Size(HitSize, HitSize);
        HandleCreated += (_, _) => InvisibleOleWindow.Apply(Handle, HitAlpha);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080  // WS_EX_TOOLWINDOW
                          | 0x08000000  // WS_EX_NOACTIVATE
                          | 0x00000008; // WS_EX_TOPMOST
            return cp;
        }
    }

    public void Prepare()
    {
        if (!IsHandleCreated)
            CreateControl();
        InvisibleOleWindow.Apply(Handle, HitAlpha);
    }

    public void FollowCursor(int screenX, int screenY)
    {
        Prepare();
        InvisibleOleWindow.Apply(Handle, HitAlpha);
        InvisibleOleWindow.PlaceTopMost(
            Handle,
            screenX - HitSize / 2,
            screenY - HitSize / 2,
            HitSize,
            HitSize);
        if (!Visible)
            Show();
    }

    public void Withdraw()
    {
        if (Visible)
            Hide();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == InvisibleOleWindow.WmNcHitTest)
        {
            m.Result = (IntPtr)InvisibleOleWindow.HtClient;
            return;
        }

        base.WndProc(ref m);
    }
}
