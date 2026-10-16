using RdmNet.Forms;

namespace RdmNet;

internal static class Program
{
    private const string SingleInstanceMutexName = "RdmNet.SingleInstance.v1";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("RdmNet 已在运行。", "RdmNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    private static void ShowError(Exception? exception)
    {
        MessageBox.Show(exception?.Message ?? "发生未知错误。", "RdmNet", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
