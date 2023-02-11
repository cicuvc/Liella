using System;

namespace System.Reflection
{
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyCompanyAttribute : Attribute
	{
		public string Company { get; }

		public AssemblyCompanyAttribute(string company)
		{
			Company = company;
		}
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyConfigurationAttribute : Attribute
	{
		public string Configuration { get; }

		public AssemblyConfigurationAttribute(string configuration)
		{
			Configuration = configuration;
		}
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyFileVersionAttribute : Attribute
	{
		public string Version { get; }

		public AssemblyFileVersionAttribute(string version)
		{
			Version = version;
		}
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyInformationalVersionAttribute : Attribute
	{
		public string InformationalVersion { get; }

		public AssemblyInformationalVersionAttribute(string informationalVersion)
		{
			InformationalVersion = informationalVersion;
		}
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyProductAttribute : Attribute
	{
		public string Product { get; }

		public AssemblyProductAttribute(string product)
		{
			Product = product;
		}
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyTitleAttribute : Attribute
	{
		public string Title { get; }

		public AssemblyTitleAttribute(string title)
		{
			Title = title;
		}
	}
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
	public sealed class AssemblyVersionAttribute : Attribute
	{
		public string Version { get; }

		public AssemblyVersionAttribute(string version)
		{
			Version = version;
		}
	}

}
