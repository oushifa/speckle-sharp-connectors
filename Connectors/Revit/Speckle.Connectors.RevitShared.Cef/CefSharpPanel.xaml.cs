using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using CefSharp;
using Speckle.Connectors.DUI.Bridge;

namespace Speckle.Connectors.Revit;

public partial class CefSharpPanel : Page, Autodesk.Revit.UI.IDockablePaneProvider, IBrowserScriptExecutor
{
  public CefSharpPanel()
  {
    InitializeComponent();
    Browser.PreviewKeyDown += OnBrowserPreviewKeyDown;
  }

  /// <inheritdoc/>
  public void ExecuteScript(string script, CancellationToken cancellationToken)
  {
    if (!Browser.CheckAccess())
    {
      ExecuteScriptDispatched(script, cancellationToken);
      return;
    }

    //avoid exceptions by checking if IBrowser is there
    if (!Browser.IsBrowserInitialized || Browser.GetBrowser() is null)
    {
      return;
    }

    Browser.ExecuteScriptAsync(script);
  }

  /// <inheritdoc/>
  public void ExecuteScriptDispatched(string script, CancellationToken cancellationToken)
  {
    if (Browser == null || !Browser.IsInitialized)
    {
      throw new InvalidOperationException("Failed to execute script, ChromiumWebBrowser is not initialized yet");
    }

    //Intentionally using the dispatcher even from the main thread
    //As it allows the UI to pump messages, and stay responsive
    Browser.Dispatcher.Invoke(
      () =>
      {
        //avoid exceptions by checking if IBrowser is there
        if (!Browser.IsBrowserInitialized || Browser.GetBrowser() is null)
        {
          return;
        }

        Browser.ExecuteScriptAsync(script);
      },
      DispatcherPriority.Background,
      cancellationToken
    );
  }

  public bool IsBrowserInitialized => Browser.IsBrowserInitialized;
  public object BrowserElement => Browser;

  public void ShowDevTools()
  {
    if (!Browser.CheckAccess())
    {
      Browser.Dispatcher.Invoke(() => ShowDevTools(), DispatcherPriority.Background);
      return;
    }

    if (!Browser.IsBrowserInitialized || Browser.GetBrowser() is null)
    {
      return;
    }

    Browser.ShowDevTools();
  }

  private void OnBrowserPreviewKeyDown(object sender, KeyEventArgs e)
  {
    bool isF12 = e.Key == Key.F12;
    bool isCtrlShiftI =
      e.Key == Key.I
      && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
      && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
    if (!isF12 && !isCtrlShiftI)
    {
      return;
    }

    ShowDevTools();
    e.Handled = true;
  }

  public void SetupDockablePane(Autodesk.Revit.UI.DockablePaneProviderData data)
  {
    data.FrameworkElement = this;
    data.InitialState = new Autodesk.Revit.UI.DockablePaneState
    {
      DockPosition = DockPosition.Tabbed,
      TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
    };
  }
}
