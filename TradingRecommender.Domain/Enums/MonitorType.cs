namespace TradingRecommender.Domain.Enums;

/// <summary>
/// Tipe monitoring data.
/// </summary>
public enum MonitorType
{
    /// <summary>
    /// Monitoring arus asing (foreign flow) di IHSG.
    /// </summary>
    ForeignFlow,

    /// <summary>
    /// Monitoring volume transaksi IHSG.
    /// </summary>
    Volume,

    /// <summary>
    /// Monitoring performa emiten dalam portofolio.
    /// </summary>
    EmitenPerformance
}
