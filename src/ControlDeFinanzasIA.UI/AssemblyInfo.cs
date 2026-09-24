using System.Runtime.CompilerServices;

// F-015 (slice S4): exposes NavigationService's internal test-only constructor overload
// (a custom route map) so NavigationServiceTests can exercise the real NavigateCore/GoBack
// code paths with a recording INavigationAware test double, without changing any production
// view-model's behavior. No production code is made visible beyond what is already public.
[assembly: InternalsVisibleTo("ControlDeFinanzasIA.Tests.Unit")]
