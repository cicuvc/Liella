using System;

namespace System.Runtime.Versioning
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
	public sealed class TargetFrameworkAttribute : Attribute
	{
		private string m_FrameworkName;

		public string FrameworkDisplayName
		{
			get
			{
				return m_FrameworkName;
			}
			set
			{
				m_FrameworkName = value;
			}
		}

		public string FrameworkName => m_FrameworkName;

		public TargetFrameworkAttribute(string frameworkName)
		{
			m_FrameworkName = frameworkName;
		}
	}
}