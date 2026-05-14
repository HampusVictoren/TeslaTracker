namespace TeslaTracker.Functions.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class RequireTurnstileAttribute : Attribute;
