#if !REVIT2026_OR_GREATER
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using CefSharp;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Connectors.Common;
using Speckle.Connectors.DUI.Bindings;
using Speckle.Connectors.DUI.Bridge;
using Speckle.Connectors.Revit.Common;
using Speckle.Converters.RevitShared.Helpers;
using Speckle.Sdk;

namespace Speckle.Connectors.Revit.Plugin;

internal sealed class RevitCefPlugin : IRevitPlugin
{
#if REVIT2020
  /// <summary>
  /// Initializes CEF with settings that are safe inside the Revit 2020 process.
  /// </summary>
  /// <remarks>
  /// CefSharp's <see cref="CefSharp.Wpf.ChromiumWebBrowser"/> auto-initializes CEF with default settings when
  /// the first browser is created. Those defaults enable GPU acceleration (and the Chromium sandbox), which is a
  /// common cause of the Revit process crashing natively on load with an "unhandled exception" dialog (the crash
  /// surfaces as a fatal error that bypasses managed try/catch). We therefore initialize CEF explicitly — before
  /// any browser is constructed — disabling GPU features and the sandbox, and writing a CEF log for diagnostics.
  /// </remarks>
  public static void InitializeCef()
  {
    if (Cef.IsInitialized)
    {
      return;
    }

    string logPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "Speckle",
      "Logs",
      "cef-revit2020.log"
    );
    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

    // Note: for .NET Framework targets CefSharp exposes the settings class as CefSharp.Wpf.CefSettings
    // (CefSharp.CefSettings only exists in the netcoreapp3.0 build of CefSharp.Common).
    var settings = new CefSharp.Wpf.CefSettings { LogFile = logPath, LogSeverity = LogSeverity.Info };
    settings.CefCommandLineArgs["disable-gpu"] = "1";
    settings.CefCommandLineArgs["disable-gpu-compositing"] = "1";
    settings.CefCommandLineArgs["no-sandbox"] = "1";

    if (!Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null))
    {
      throw new InvalidOperationException("Failed to initialize CEF for the Revit 2020 connector.");
    }
  }
#endif

  private readonly UIControlledApplication _uIControlledApplication;
  private readonly IServiceProvider _serviceProvider; // should be lazy to ensure the bindings are not created too early
  private readonly BindingOptions _bindingOptions;
  private readonly RevitContext _revitContext;
  private readonly CefSharpPanel _cefSharpPanel;
  private readonly ISpeckleApplication _speckleApplication;

  public RevitCefPlugin(
    UIControlledApplication uIControlledApplication,
    IServiceProvider serviceProvider,
    BindingOptions bindingOptions,
    RevitContext revitContext,
    CefSharpPanel cefSharpPanel,
    ISpeckleApplication speckleApplication
  )
  {
    _uIControlledApplication = uIControlledApplication;
    _serviceProvider = serviceProvider;
    _bindingOptions = bindingOptions;
    _revitContext = revitContext;
    _cefSharpPanel = cefSharpPanel;
    _speckleApplication = speckleApplication;
  }

  public void Initialise()
  {
    // Create and register panels before app initialized. this is needed for double-click file open
    CreateTabAndRibbonPanel(_uIControlledApplication);
    RegisterDockablePane();
    _uIControlledApplication.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
  }

  public void Shutdown()
  {
    // POC: should we be cleaning up the RibbonPanel etc...
    // Should we be indicating to any active in-flight functions that we are being closed?
  }

  // POC: Could be injected but maybe not worthwhile
  private void CreateTabAndRibbonPanel(UIControlledApplication application)
  {
    // POC: some top-level handling and feedback here
    try
    {
      application.CreateRibbonTab(Connector.TabName);
    }
    catch (ArgumentException)
    {
      // exception occurs when the speckle tab has already been created.
      // this happens when both the dui2 and the dui3 connectors are installed. Can be safely ignored.
    }

    RibbonPanel specklePanel = application.CreateRibbonPanel(Connector.TabName, Connector.TabTitle);
    var dui3Button = (PushButton)
      specklePanel.AddItem(
        new PushButtonData(
          _speckleApplication.HostApplication,
          Connector.TabTitle,
          typeof(RevitExternalApplication).Assembly.Location,
          typeof(SpeckleRevitCommand).FullName
        )
      );

    string path = typeof(RevitCefPlugin).Assembly.Location;
    dui3Button.Image = LoadPngImgSource(
      $"Speckle.Connectors.Revit{_speckleApplication.HostApplicationVersion}.Assets.logo16.png",
      path
    );
    dui3Button.LargeImage = LoadPngImgSource(
      $"Speckle.Connectors.Revit{_speckleApplication.HostApplicationVersion}.Assets.logo32.png",
      path
    );
    dui3Button.ToolTipImage = LoadPngImgSource(
      $"Speckle.Connectors.Revit{_speckleApplication.HostApplicationVersion}.Assets.logo32.png",
      path
    );
    dui3Button.ToolTip = "数智南北";
    //dui3Button.AvailabilityClassName = typeof(CmdAvailabilityViews).FullName;
    dui3Button.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, "https://speckle.systems"));
  }

  private void OnApplicationInitialized(object? sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
  {
    var uiApplication = new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application);
    _revitContext.UIApplication = uiApplication;

    // POC: might be worth to interface this out, we shall see...
    RevitAsync.Initialize(uiApplication);

    PostApplicationInit(); // for double-click file open
  }

  /// <summary>
  /// Actions to run after UiApplication initialized. This is needed for double-click file open issue.
  /// </summary>
  private void PostApplicationInit()
  {
    var bindings = _serviceProvider.GetRequiredService<IEnumerable<IBinding>>();
    // binding the bindings to each bridge
    foreach (IBinding binding in bindings)
    {
      Debug.WriteLine(binding.Name);
      binding.Parent.AssociateWithBinding(binding);
    }

    _cefSharpPanel.Browser.IsBrowserInitializedChanged += (sender, e) =>
    {
      if (e.NewValue is false)
      {
        return;
      }

#if DEBUG || LOCAL
      _cefSharpPanel.Browser.ShowDevTools();
#endif
      foreach (IBinding binding in bindings)
      {
        IBrowserBridge bridge = binding.Parent;

#if REVIT2025_OR_GREATER
        _cefSharpPanel.Browser.JavascriptObjectRepository.Register(bridge.FrontendBoundName, bridge, _bindingOptions);
#else
        _cefSharpPanel.Browser.JavascriptObjectRepository.Register(
          bridge.FrontendBoundName,
          bridge,
          true,
          _bindingOptions
        );
#endif
      }
    };
  }

  private void RegisterDockablePane()
  {
    CefSharpSettings.ConcurrentTaskExecution = true;

    // Registering dockable pane should happen before UiApplication is initialized with RevitTask.
    // Otherwise pane cannot be registered for double-click file open.
    _uIControlledApplication.RegisterDockablePane(RevitExternalApplication.DockablePanelId, "数智南北", _cefSharpPanel);
  }

  private ImageSource? LoadPngImgSource(string sourceName, string path)
  {
    try
    {
      var assembly = Assembly.LoadFrom(Path.Combine(path));
      var icon = assembly.GetManifestResourceStream(sourceName);
      PngBitmapDecoder decoder = new(icon, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
      ImageSource source = decoder.Frames[0];
      return source;
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      // POC: logging
    }

    return null;
  }
}
#endif
