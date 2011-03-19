﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using N2.Definitions.Static;
using N2.Engine;
using N2.Persistence;

namespace N2.Definitions.Runtime
{
	[Service(typeof(ITemplateProvider))]
	public class ViewTemplateProvider : ITemplateProvider
	{
		IProvider<HttpContextBase> httpContextProvider;
		IProvider<VirtualPathProvider> vppProvider;
		ContentActivator activator;
		DefinitionBuilder builder;
		ViewTemplateRegistrator registrator;
		ViewTemplateAnalyzer analyzer;
		List<ViewTemplateSource> sources = new List<ViewTemplateSource>();
		bool rebuild = true;

		public ViewTemplateProvider(ViewTemplateRegistrator registrator, ViewTemplateAnalyzer analyzer, ContentActivator activator, DefinitionBuilder builder, IProvider<HttpContextBase> httpContextProvider, IProvider<VirtualPathProvider> vppProvider)
		{
			this.registrator = registrator;
			this.analyzer = analyzer;
			this.activator = activator;
			this.builder = builder;
			this.httpContextProvider = httpContextProvider;
			this.vppProvider = vppProvider;

			registrator.RegistrationAdded += (s, a) => HandleRegistrationQueue();
			HandleRegistrationQueue();
		}

		private void HandleRegistrationQueue()
		{
			while (registrator.QueuedRegistrations.Count > 0)
			{
				sources.Add(registrator.QueuedRegistrations.Dequeue());
				rebuild = true;
			}
		}

		#region ITemplateProvider Members

		public IEnumerable<TemplateDefinition> GetTemplates(Type contentType)
		{
			var httpContext = httpContextProvider.Get();
			try
			{
				httpContext.Request.GetType();
			}
			catch (Exception)
			{
				return new TemplateDefinition[0];
			}
			
			const string cacheKey = "RazorDefinitions";
			var definitions = httpContext.Cache[cacheKey] as IEnumerable<ItemDefinition>;
			if (definitions == null || rebuild)
			{
				var vpp = vppProvider.Get();
				var registrations = analyzer.FindRegistrations(vpp, httpContext, sources).ToList();
				definitions = BuildDefinitions(registrations);

				var files = registrations.SelectMany(p => p.TouchedPaths).Distinct().ToList();
				//var dirs = files.Select(f => f.Substring(0, f.LastIndexOf('/'))).Distinct();
				var cacheDependency = vpp.GetCacheDependency(files.FirstOrDefault(), files, DateTime.UtcNow);

				httpContext.Cache.Remove(cacheKey);
				httpContext.Cache.Add(cacheKey, definitions, cacheDependency, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.AboveNormal, new CacheItemRemovedCallback(delegate { Debug.WriteLine("Razor template changed"); }));
				rebuild = false;
			}
			var templates = definitions.Where(d => d.ItemType == contentType).Select(d =>
				{
					var td = new TemplateDefinition();
					td.Definition = d;
					td.Description = d.Description;
					td.Name = d.Template;
					td.Original = null;
					td.Template = activator.CreateInstance(d.ItemType, null);
					td.Template["TemplateName"] = d.Template;
					td.TemplateUrl = null;
					td.Title = d.Title;
					td.ReplaceDefault = "Index".Equals(d.Template, StringComparison.InvariantCultureIgnoreCase);
					return td;
				}).ToArray();

			foreach (var t in templates)
			{					
				t.Definition.Add(new TemplateSelectorAttribute { Name = "TemplateName", Title = "Template", AllTemplates = templates, ContainerName = "Advanced", Required = true, HelpTitle = "The page must be saved for another template's fields to appear" });
			}
			

			return templates;
		}

		public TemplateDefinition GetTemplate(N2.ContentItem item)
		{
			string templateName = item["TemplateName"] as string;
			if (templateName == null)
				return null;

			return GetTemplates(item.GetContentType()).Where(t => t.Name == templateName).Select(t =>
				{
					t.Original = t.Template;
					t.Template = item;
					return t;
				}).FirstOrDefault();
		}

		private IEnumerable<ItemDefinition> BuildDefinitions(List<ViewTemplateDescription> registrations)
		{
			var definitions = registrations.Select(r => r.Definition).ToList();
			builder.ExecuteRefiners(definitions);
			foreach (var registration in registrations)
				registration.Registration.AppendDefinition(registration.Definition);
			return definitions;
		}

		#endregion
	}
}