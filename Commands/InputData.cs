using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace SkribeSeinSDBot.Commands;

public record InputData
{
	public InputData(string originalQuery)
	{
		OriginalQuery = originalQuery;
	}

	public string OriginalQuery { get;  }
	public bool InPublic { get; set; }

	[DisplayName("Prompt")]
	[DefaultValue("")]
	public string Positive { get; set; }
	[DisplayName("Negative")]
	[DefaultValue("")]
	public string Negative { get; set; }
	[DisplayName("SizeX")]
	public int SizeX { get; set; } = 512;
	[DisplayName("SizeY")]
	public int SizeY { get; set; } = 512;
	[DisplayName("BatchSize")]
	[DefaultValue(1)]
	public int BatchSize { get; set; } = 1;
	[DisplayName("Steps")]
	public int Steps { get; set; } = 50;
	[DisplayName("Model")]
	[DefaultValue("")]
	public string Model { get; set; }
	[DisplayName("Weight")]
	public int Cfg { get; set; } = 8; 
	[DisplayName("SamplerName")]
	[DefaultValue("")]
	public string Sampler { get; set; }
	[DisplayName("Upscale Pass")]
	[DefaultValue(1)]
	public int UpscalePass { get; set; } = 1;
	[DisplayName("Upscale Scale")]
	[DefaultValue(1)]
	public double UpscaleScale { get; set; } = 1; 
	[DisplayName("Upscaler")]
	[DefaultValue("")]
	public string Upscaler { get; set; }
	[DisplayName("Denoise Strength")]
	[DefaultValue(1)]
	public double DenoiseStrength { get; set; }
	[DisplayName("Batch Count")]
	[DefaultValue(1)]
	public int BatchCount { get; set; }
	[DisplayName("Seed")]
	[DefaultValue(-1)]
	public long Seed { get; set; }
	[DisplayName("Face Restore")]
	[DefaultValue(false)]
	public bool FaceRestore { get; set; }

	[DisplayName("Clip Skip")]
	[DefaultValue(0)]
	public int ClipSkip { get; set; }

	[DisplayName("Upscale Steps")]
	[DefaultValue(0)]
	public int UpscalerSteps { get; set; }

	[DisplayName("Upscale Sampler")]
	[DefaultValue(null)]
	public string UpscaleSampler { get; set; }

	public override string ToString()
	{
		return string.Join(", ", GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(e => e.GetCustomAttribute<DisplayNameAttribute>() != null)
			.Select(e =>
			{
				var name = e.GetCustomAttribute<DisplayNameAttribute>();
				var value = e.GetValue(this);
				if (value is null)
				{
					return null;
				}

				var defaultValue = e.GetCustomAttribute<DefaultValueAttribute>()?.Value;

				if (value.Equals(defaultValue))
				{
					return null;
				}

				return $"{name.DisplayName}: <code>{HttpUtility.HtmlEncode(value.ToString()?.Replace('`', '\"'))}</code>";
			})
			.Where(e => e is not null));

		return base.ToString();
	}
}