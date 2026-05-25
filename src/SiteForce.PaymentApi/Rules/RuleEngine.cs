using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

/// <summary>
/// Microkernel core: Orchestrates the execution of registered rule plugins
/// against attendance records to produce payment calculations.
/// </summary>
public class RuleEngine
{
    private readonly IEnumerable<IRulePlugin> _plugins;

    public RuleEngine(IEnumerable<IRulePlugin> plugins)
    {
        _plugins = plugins.OrderBy(p => p.Priority);
    }

    /// <summary>
    /// Executes all registered rule plugins against the given attendance record.
    /// </summary>
    public CalculationContext Execute(AttendanceRecord record, SiteRuleConfig? siteConfig = null)
    {
        var context = new CalculationContext
        {
            SiteConfig = siteConfig
        };

        foreach (var plugin in _plugins)
        {
            var result = plugin.Execute(record, context);
            context.AppliedRules.Add(result);
        }

        return context;
    }
}
