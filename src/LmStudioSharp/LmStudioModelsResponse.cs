using System.Text.Json.Serialization;

namespace LmStudioSharp;

public sealed class LmStudioModelsResponse
{
    [JsonPropertyName("models")]
    public List<LmStudioModel> Models { get; set; } = [];
}

public sealed class LmStudioModel
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("quantization")]
    public LmStudioQuantization? Quantization { get; set; }

    [JsonPropertyName("size_bytes")]
    public long? SizeBytes { get; set; }

    [JsonPropertyName("params_string")]
    public string? ParamsString { get; set; }

    [JsonPropertyName("loaded_instances")]
    public List<LmStudioLoadedInstance> LoadedInstances { get; set; } = [];

    [JsonPropertyName("max_context_length")]
    public int? MaxContextLength { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("capabilities")]
    public LmStudioCapabilities? Capabilities { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("variants")]
    public List<string> Variants { get; set; } = [];

    [JsonPropertyName("selected_variant")]
    public string? SelectedVariant { get; set; }
}

public sealed class LmStudioQuantization
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("bits_per_weight")]
    public int? BitsPerWeight { get; set; }
}

public sealed class LmStudioCapabilities
{
    [JsonPropertyName("vision")]
    public bool? Vision { get; set; }

    [JsonPropertyName("trained_for_tool_use")]
    public bool? TrainedForToolUse { get; set; }

    [JsonPropertyName("reasoning")]
    public LmStudioReasoning? Reasoning { get; set; }
}

public sealed class LmStudioReasoning
{
    [JsonPropertyName("allowed_options")]
    public List<string> AllowedOptions { get; set; } = [];

    [JsonPropertyName("default")]
    public string? Default { get; set; }
}

public sealed class LmStudioLoadedInstance
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("config")]
    public LmStudioLoadedInstanceConfig? Config { get; set; }
}

public sealed class LmStudioLoadedInstanceConfig
{
    [JsonPropertyName("context_length")]
    public int? ContextLength { get; set; }

    [JsonPropertyName("eval_batch_size")]
    public int? EvalBatchSize { get; set; }

    [JsonPropertyName("parallel")]
    public int? Parallel { get; set; }

    [JsonPropertyName("flash_attention")]
    public bool? FlashAttention { get; set; }

    [JsonPropertyName("num_experts")]
    public int? NumExperts { get; set; }

    [JsonPropertyName("offload_kv_cache_to_gpu")]
    public bool? OffloadKvCacheToGpu { get; set; }
}

