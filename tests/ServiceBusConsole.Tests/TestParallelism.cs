using TUnit.Core.Helpers;

// HeadlessTerminal tests wait on renders with timeouts; at TUnit's default of 4 tests per core they starve each other.
// Each UI test class is also [NotInParallel] on its own key, so its tests run one at a time, as under xunit.
[assembly: ParallelLimiter<ProcessorCountParallelLimit>]
