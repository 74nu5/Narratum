using System.Diagnostics;
using Narratum.Orchestration.Stages;

namespace Narratum.Orchestration.Logging;

/// <summary>
/// Type de métrique collectée.
/// </summary>
public enum MetricType
{
    /// <summary>
    /// Durée d'exécution.
    /// </summary>
    Duration,

    /// <summary>
    /// Compteur (nombre d'occurrences).
    /// </summary>
    Counter,

    /// <summary>
    /// Jauge (valeur instantanée).
    /// </summary>
    Gauge,

    /// <summary>
    /// Histogramme (distribution de valeurs).
    /// </summary>
    Histogram
}

/// <summary>
/// Point de données métrique.
/// </summary>
public sealed record MetricDataPoint(
    string Name,
    MetricType Type,
    double Value,
    DateTime Timestamp,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>
    /// Crée un point de données de durée.
    /// </summary>
    public static MetricDataPoint Duration(
        string name,
        TimeSpan duration,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        return new MetricDataPoint(
            name,
            MetricType.Duration,
            duration.TotalMilliseconds,
            DateTime.UtcNow,
            tags ?? new Dictionary<string, string>());
    }

    /// <summary>
    /// Crée un point de données de compteur.
    /// </summary>
    public static MetricDataPoint Counter(
        string name,
        long count,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        return new MetricDataPoint(
            name,
            MetricType.Counter,
            count,
            DateTime.UtcNow,
            tags ?? new Dictionary<string, string>());
    }

    /// <summary>
    /// Crée un point de données de jauge.
    /// </summary>
    public static MetricDataPoint Gauge(
        string name,
        double value,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        return new MetricDataPoint(
            name,
            MetricType.Gauge,
            value,
            DateTime.UtcNow,
            tags ?? new Dictionary<string, string>());
    }
}

/// <summary>
/// Statistiques agrégées pour une métrique.
/// </summary>
public sealed record MetricStatistics(
    string Name,
    int Count,
    double Min,
    double Max,
    double Average,
    double Percentile50,
    double Percentile95,
    double Percentile99)
{
    /// <summary>
    /// Calcule les statistiques à partir d'une série de valeurs.
    /// </summary>
    public static MetricStatistics Calculate(string name, IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return new MetricStatistics(name, 0, 0, 0, 0, 0, 0, 0);
        }

        var sorted = values.OrderBy(v => v).ToList();
        var count = sorted.Count;

        return new MetricStatistics(
            name,
            count,
            sorted[0],
            sorted[^1],
            sorted.Average(),
            GetPercentile(sorted, 50),
            GetPercentile(sorted, 95),
            GetPercentile(sorted, 99));
    }

    private static double GetPercentile(IReadOnlyList<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }
}

/// <summary>
/// Résumé des métriques du pipeline.
/// </summary>
public sealed record PipelineMetricsSummary(
    Guid PipelineId,
    TimeSpan TotalDuration,
    IReadOnlyDictionary<string, TimeSpan> StageDurations,
    IReadOnlyDictionary<AgentType, TimeSpan> AgentDurations,
    int TotalAgentCalls,
    int RetryCount,
    bool Success)
{
    /// <summary>
    /// Durée moyenne par étape.
    /// </summary>
    public TimeSpan AverageStageDuration =>
        StageDurations.Count > 0
            ? TimeSpan.FromMilliseconds(StageDurations.Values.Average(d => d.TotalMilliseconds))
            : TimeSpan.Zero;

    /// <summary>
    /// Étape la plus lente.
    /// </summary>
    public string? SlowestStage =>
        StageDurations.Count > 0
            ? StageDurations.MaxBy(kvp => kvp.Value).Key
            : null;

    /// <summary>
    /// Agent le plus lent.
    /// </summary>
    public AgentType? SlowestAgent =>
        AgentDurations.Count > 0
            ? AgentDurations.MaxBy(kvp => kvp.Value).Key
            : null;
}

/// <summary>
/// Collecteur de métriques de performance pour le pipeline.
///
/// Permet de mesurer les durées d'exécution, compter les événements,
/// et générer des statistiques agrégées.
/// </summary>
public sealed class MetricsCollector
{
    private readonly List<MetricDataPoint> _dataPoints = new();
    private readonly Dictionary<Guid, PipelineMetricsBuilder> _pipelineBuilders = new();
    private readonly object _lock = new();
    private readonly MetricsCollectorConfig _config;

    public MetricsCollector(MetricsCollectorConfig? config = null)
    {
        _config = config ?? MetricsCollectorConfig.Default;
    }

    /// <summary>
    /// Enregistre le début d'un pipeline.
    /// </summary>
    public void StartPipeline(Guid pipelineId)
    {
        lock (_lock)
        {
            _pipelineBuilders[pipelineId] = new PipelineMetricsBuilder(pipelineId);
        }
    }

    /// <summary>
    /// Enregistre la fin d'un pipeline.
    /// </summary>
    public PipelineMetricsSummary EndPipeline(Guid pipelineId, bool success)
    {
        lock (_lock)
        {
            if (!_pipelineBuilders.TryGetValue(pipelineId, out var builder))
            {
                throw new InvalidOperationException($"Pipeline {pipelineId} not found");
            }

            var summary = builder.Build(success);

            // Enregistrer les métriques
            RecordDataPoint(MetricDataPoint.Duration(
                "pipeline.duration",
                summary.TotalDuration,
                new Dictionary<string, string>
                {
                    ["pipeline_id"] = pipelineId.ToString(),
                    ["success"] = success.ToString()
                }));

            RecordDataPoint(MetricDataPoint.Counter(
                "pipeline.agent_calls",
                summary.TotalAgentCalls,
                new Dictionary<string, string> { ["pipeline_id"] = pipelineId.ToString() }));

            if (summary.RetryCount > 0)
            {
                RecordDataPoint(MetricDataPoint.Counter(
                    "pipeline.retries",
                    summary.RetryCount,
                    new Dictionary<string, string> { ["pipeline_id"] = pipelineId.ToString() }));
            }

            _pipelineBuilders.Remove(pipelineId);
            return summary;
        }
    }

    /// <summary>
    /// Enregistre le début d'une étape.
    /// </summary>
    public void StartStage(Guid pipelineId, string stageName)
    {
        lock (_lock)
        {
            if (_pipelineBuilders.TryGetValue(pipelineId, out var builder))
            {
                builder.StartStage(stageName);
            }
        }
    }

    /// <summary>
    /// Enregistre la fin d'une étape.
    /// </summary>
    public void EndStage(Guid pipelineId, string stageName)
    {
        lock (_lock)
        {
            if (_pipelineBuilders.TryGetValue(pipelineId, out var builder))
            {
                var duration = builder.EndStage(stageName);

                RecordDataPoint(MetricDataPoint.Duration(
                    $"stage.{stageName.ToLowerInvariant()}.duration",
                    duration,
                    new Dictionary<string, string>
                    {
                        ["pipeline_id"] = pipelineId.ToString(),
                        ["stage"] = stageName
                    }));
            }
        }
    }

    /// <summary>
    /// Enregistre un appel d'agent.
    /// </summary>
    public void RecordAgentCall(Guid pipelineId, AgentType agent, TimeSpan duration, bool success)
    {
        lock (_lock)
        {
            if (_pipelineBuilders.TryGetValue(pipelineId, out var builder))
            {
                builder.RecordAgentCall(agent, duration);
            }

            RecordDataPoint(MetricDataPoint.Duration(
                $"agent.{agent.ToString().ToLowerInvariant()}.duration",
                duration,
                new Dictionary<string, string>
                {
                    ["pipeline_id"] = pipelineId.ToString(),
                    ["agent"] = agent.ToString(),
                    ["success"] = success.ToString()
                }));
        }
    }

    /// <summary>
    /// Enregistre un retry.
    /// </summary>
    public void RecordRetry(Guid pipelineId, int attemptNumber)
    {
        lock (_lock)
        {
            if (_pipelineBuilders.TryGetValue(pipelineId, out var builder))
            {
                builder.RecordRetry();
            }

            IncrementCounter("pipeline.retry.count");
        }
    }

    /// <summary>
    /// Enregistre un point de données arbitraire.
    /// </summary>
    public void RecordDataPoint(MetricDataPoint dataPoint)
    {
        lock (_lock)
        {
            // Respecter la limite max
            if (_config.MaxDataPointsToRetain > 0 && _dataPoints.Count >= _config.MaxDataPointsToRetain)
            {
                var toRemove = _dataPoints.Count - _config.MaxDataPointsToRetain + 1;
                _dataPoints.RemoveRange(0, toRemove);
            }

            _dataPoints.Add(dataPoint);
        }
    }

    /// <summary>
    /// Incrémente un compteur.
    /// </summary>
    public void IncrementCounter(string name, IReadOnlyDictionary<string, string>? tags = null)
    {
        RecordDataPoint(MetricDataPoint.Counter(name, 1, tags));
    }

    /// <summary>
    /// Enregistre une durée.
    /// </summary>
    public void RecordDuration(string name, TimeSpan duration, IReadOnlyDictionary<string, string>? tags = null)
    {
        RecordDataPoint(MetricDataPoint.Duration(name, duration, tags));
    }

    /// <summary>
    /// Enregistre une valeur de jauge.
    /// </summary>
    public void RecordGauge(string name, double value, IReadOnlyDictionary<string, string>? tags = null)
    {
        RecordDataPoint(MetricDataPoint.Gauge(name, value, tags));
    }

    /// <summary>
    /// Mesure la durée d'exécution d'une action.
    /// </summary>
    public T Measure<T>(string metricName, Func<T> action, IReadOnlyDictionary<string, string>? tags = null)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return action();
        }
        finally
        {
            stopwatch.Stop();
            RecordDuration(metricName, stopwatch.Elapsed, tags);
        }
    }

    /// <summary>
    /// Mesure la durée d'exécution d'une action asynchrone.
    /// </summary>
    public async Task<T> MeasureAsync<T>(
        string metricName,
        Func<Task<T>> action,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            stopwatch.Stop();
            RecordDuration(metricName, stopwatch.Elapsed, tags);
        }
    }

    /// <summary>
    /// Récupère tous les points de données.
    /// </summary>
    public IReadOnlyList<MetricDataPoint> GetDataPoints()
    {
        lock (_lock)
        {
            return _dataPoints.ToList();
        }
    }

    /// <summary>
    /// Récupère les points de données pour une métrique spécifique.
    /// </summary>
    public IReadOnlyList<MetricDataPoint> GetDataPoints(string metricName)
    {
        lock (_lock)
        {
            return _dataPoints
                .Where(dp => dp.Name.Equals(metricName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Récupère les statistiques pour une métrique.
    /// </summary>
    public MetricStatistics GetStatistics(string metricName)
    {
        var values = GetDataPoints(metricName).Select(dp => dp.Value).ToList();
        return MetricStatistics.Calculate(metricName, values);
    }

    /// <summary>
    /// Récupère les statistiques pour toutes les métriques.
    /// </summary>
    public IReadOnlyDictionary<string, MetricStatistics> GetAllStatistics()
    {
        lock (_lock)
        {
            return _dataPoints
                .GroupBy(dp => dp.Name)
                .ToDictionary(
                    g => g.Key,
                    g => MetricStatistics.Calculate(g.Key, g.Select(dp => dp.Value).ToList()));
        }
    }

    /// <summary>
    /// Efface toutes les données.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _dataPoints.Clear();
            _pipelineBuilders.Clear();
        }
    }

    /// <summary>
    /// Nombre de points de données.
    /// </summary>
    public int DataPointCount
    {
        get
        {
            lock (_lock)
            {
                return _dataPoints.Count;
            }
        }
    }

    /// <summary>
    /// Génère un rapport de métriques.
    /// </summary>
    public MetricsReport GenerateReport()
    {
        var allStats = GetAllStatistics();
        return new MetricsReport(DateTime.UtcNow, allStats);
    }

    /// <summary>
    /// Builder interne pour les métriques de pipeline.
    /// </summary>
    private sealed class PipelineMetricsBuilder
    {
        private readonly Guid _pipelineId;
        private readonly Stopwatch _totalStopwatch;
        private readonly Dictionary<string, Stopwatch> _stageStopwatches = new();
        private readonly Dictionary<string, TimeSpan> _stageDurations = new();
        private readonly Dictionary<AgentType, TimeSpan> _agentDurations = new();
        private int _agentCallCount;
        private int _retryCount;

        public PipelineMetricsBuilder(Guid pipelineId)
        {
            _pipelineId = pipelineId;
            _totalStopwatch = Stopwatch.StartNew();
        }

        public void StartStage(string stageName)
        {
            _stageStopwatches[stageName] = Stopwatch.StartNew();
        }

        public TimeSpan EndStage(string stageName)
        {
            if (_stageStopwatches.TryGetValue(stageName, out var stopwatch))
            {
                stopwatch.Stop();
                _stageDurations[stageName] = stopwatch.Elapsed;
                _stageStopwatches.Remove(stageName);
                return stopwatch.Elapsed;
            }
            return TimeSpan.Zero;
        }

        public void RecordAgentCall(AgentType agent, TimeSpan duration)
        {
            _agentCallCount++;

            if (_agentDurations.TryGetValue(agent, out var existing))
            {
                _agentDurations[agent] = existing + duration;
            }
            else
            {
                _agentDurations[agent] = duration;
            }
        }

        public void RecordRetry()
        {
            _retryCount++;
        }

        public PipelineMetricsSummary Build(bool success)
        {
            _totalStopwatch.Stop();

            return new PipelineMetricsSummary(
                _pipelineId,
                _totalStopwatch.Elapsed,
                _stageDurations,
                _agentDurations,
                _agentCallCount,
                _retryCount,
                success);
        }
    }
}

/// <summary>
/// Rapport de métriques.
/// </summary>
public sealed record MetricsReport(
    DateTime GeneratedAt,
    IReadOnlyDictionary<string, MetricStatistics> Statistics)
{
    /// <summary>
    /// Génère une représentation textuelle du rapport.
    /// </summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            "Metrics Report",
            $"Generated: {GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}",
            new string('=', 80),
            ""
        };

        if (Statistics.Count == 0)
        {
            lines.Add("No metrics collected.");
            return string.Join(Environment.NewLine, lines);
        }

        // Grouper par préfixe
        var grouped = Statistics
            .GroupBy(kvp => kvp.Key.Split('.')[0])
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            lines.Add($"[{group.Key.ToUpperInvariant()}]");
            lines.Add(new string('-', 80));

            foreach (var (name, stats) in group.OrderBy(kvp => kvp.Key))
            {
                if (stats.Count == 0) continue;

                var unit = name.Contains("duration") ? "ms" : "";
                lines.Add($"  {name}:");
                lines.Add($"    Count: {stats.Count} | " +
                         $"Avg: {stats.Average:F2}{unit} | " +
                         $"Min: {stats.Min:F2}{unit} | " +
                         $"Max: {stats.Max:F2}{unit}");
                lines.Add($"    P50: {stats.Percentile50:F2}{unit} | " +
                         $"P95: {stats.Percentile95:F2}{unit} | " +
                         $"P99: {stats.Percentile99:F2}{unit}");
            }

            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Configuration du collecteur de métriques.
/// </summary>
public sealed record MetricsCollectorConfig
{
    /// <summary>
    /// Nombre maximum de points de données à conserver.
    /// 0 signifie pas de limite.
    /// </summary>
    public int MaxDataPointsToRetain { get; init; } = 100000;

    /// <summary>
    /// Configuration par défaut.
    /// </summary>
    public static MetricsCollectorConfig Default => new();

    /// <summary>
    /// Configuration pour les tests.
    /// </summary>
    public static MetricsCollectorConfig ForTesting => new()
    {
        MaxDataPointsToRetain = 10000
    };
}
