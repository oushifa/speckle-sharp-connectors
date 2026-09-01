using System.IO;
using Autodesk.Revit.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Speckle.Connectors.Common;
using Speckle.Connectors.DUI;
using Speckle.Connectors.Revit.Common;
using Speckle.Connectors.Revit.DependencyInjection;
using Speckle.Converters.RevitShared;
using Speckle.Sdk;

namespace Speckle.Connectors.Revit.Plugin;

internal sealed class RevitExternalApplication : IExternalApplication
{
  private IRevitPlugin? _revitPlugin;

  private ServiceProvider? _container;
  private IDisposable? _disposableLogger;

  // POC: move to somewhere central?
  public static readonly DockablePaneId DockablePanelId = new(new Guid("{f7b5da7c-366c-4b13-8455-b56f433f461e}"));

  private static HostAppVersion GetVersion()
  {
#if REVIT2020
    return HostAppVersion.v2020;
#elif REVIT2022
    return HostAppVersion.v2022;
#elif REVIT2023
    return HostAppVersion.v2023;
#elif REVIT2024
    return HostAppVersion.v2024;
#elif REVIT2025
    return HostAppVersion.v2025;
#elif REVIT2026
    return HostAppVersion.v2026;
#else
    throw new NotImplementedException();
#endif
  }

  public Result OnStartup(UIControlledApplication application)
  {
    try
    {
      // POC: not sure what this is doing...  could be messing up our Aliasing????
      AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver.OnAssemblyResolve<RevitExternalApplication>;
      var services = new ServiceCollection();
      // init DI
      _disposableLogger = services.Initialize(HostApplications.Revit, GetVersion());
      services.AddRevit();
      services.AddRevitConverters();
      services.AddSingleton(application);
      _container = services.BuildServiceProvider();
      _container.UseDUI();

      RevitAsync.Initialize(application);
      // resolve root object
      _revitPlugin = _container.GetRequiredService<IRevitPlugin>();
      _revitPlugin.Initialise();
    }
    catch (Exception e) when (!e.IsFatal())
    {
      _container
        ?.GetRequiredService<ILoggerFactory>()
        .CreateLogger<RevitExternalApplication>()
        .LogCritical(e, "Unhandled exception");
      TryDumpStartupError(e);
      // POC: feedback?
      return Result.Failed;
    }

    return Result.Succeeded;
  }

  /// <summary>
  /// Fail-safe diagnostic dump: writes the startup exception to a plain-text file so we can find it even
  /// when the logging pipeline itself failed to initialize (Revit's journal only records a bare API_ERROR).
  /// </summary>
  private static void TryDumpStartupError(Exception exception)
  {
    try
    {
      string logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Speckle",
        "Logs"
      );
      Directory.CreateDirectory(logDir);
      File.WriteAllText(
        Path.Combine(logDir, "revit-startup-error.log"),
        $"{DateTime.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}"
      );
    }
    catch (Exception e) when (!e.IsFatal())
    {
      // best effort - never let diagnostics break startup/shutdown
    }
  }

  public Result OnShutdown(UIControlledApplication application)
  {
    try
    {
      // POC: could this be more a generic Connector Init() Shutdown()
      // possibly with injected pieces or with some abstract methods?
      // need to look for commonality
      _revitPlugin?.Shutdown();
      _disposableLogger?.Dispose();
      _container?.Dispose();
    }
    catch (Exception e) when (!e.IsFatal())
    {
      // POC: feedback?
      return Result.Failed;
    }

    return Result.Succeeded;
  }
}
