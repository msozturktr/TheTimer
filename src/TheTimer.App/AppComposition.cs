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
        var viewModel = new MainWindowViewModel(new SessionTimer(timeProvider), repository, activeStore, timeProvider, notifier)
        {
            Report = reportViewModel,
        };
        var plan = new PlanViewModel(planService, repository, timeProvider, notifier, viewModel.StartFromPlan);
        viewModel.Plan = plan;
        viewModel.SessionsChanged += (_, _) => plan.Refresh();
        viewModel.SessionsChanged += (_, _) => reportViewModel.Refresh();
        return viewModel;
    }
}
