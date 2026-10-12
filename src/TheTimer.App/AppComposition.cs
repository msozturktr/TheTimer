using TheTimer.App.ViewModels;
using TheTimer.Core.Notifications;
using TheTimer.Core.Planning;
using TheTimer.Core.Reports;
using TheTimer.Core.Sessions;

namespace TheTimer.App;

/// <summary>Wires the main view model with its Plan and Report view models; shared by the app and the screenshot tool.</summary>
public static class AppComposition
{
    public static MainWindowViewModel Create(
        ISessionRepository repository,
        IActiveSessionStore activeStore,
        IPlanRepository planRepository,
        TimeProvider timeProvider,
        INotifier notifier)
    {
        var planService = new PlanService(planRepository);
        var reportViewModel = new ReportViewModel(new ReportService(repository, planService), timeProvider);
        var timer = new SessionTimer(timeProvider);
        MainWindowViewModel? viewModel = null;
        viewModel = new MainWindowViewModel(timer, repository, activeStore, timeProvider, notifier)
        {
            Report = reportViewModel,
            TodayArc = new TodayArcViewModel(repository, planService, timer, timeProvider, () => viewModel?.IsRunningBreak ?? false),
        };
        var plan = new PlanViewModel(planService, repository, timeProvider, notifier, viewModel.StartFromPlan);
        viewModel.Plan = plan;
        viewModel.SessionsChanged += (_, _) => plan.Refresh();
        viewModel.SessionsChanged += (_, _) => reportViewModel.Refresh();
        viewModel.SessionsChanged += (_, _) => viewModel.TodayArc!.Refresh();
        return viewModel;
    }
}
