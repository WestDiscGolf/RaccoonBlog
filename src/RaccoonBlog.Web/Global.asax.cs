using System;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using RaccoonBlog.Web.Controllers;
using RaccoonBlog.Web.Helpers.Attributes;
using RaccoonBlog.Web.Helpers.Binders;
using RaccoonBlog.Web.Infrastructure.AutoMapper;
using RaccoonBlog.Web.Infrastructure.Indexes;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Client.MvcIntegration;

namespace RaccoonBlog.Web
{
	public class MvcApplication : HttpApplication
	{
		public static void RegisterGlobalFilters(GlobalFilterCollection filters)
		{
			//filters.Add(new ElmahHandleErrorAttribute());
		}

		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();

			RegisterGlobalFilters(GlobalFilters.Filters);
			InitializeDocumentStore();
			new RouteConfigurator(RouteTable.Routes).Configure();
			ModelBinders.Binders.Add(typeof (CommentCommandOptions), new RemoveSpacesEnumBinder());
			ModelBinders.Binders.Add(typeof (Guid), new GuidBinder());

			AutoMapperConfiguration.Configure();
		}

		public static IDocumentStore DocumentStore { get; private set; }

		private static void InitializeDocumentStore()
		{
			if (DocumentStore != null) return; // prevent misuse

			DocumentStore = new DocumentStore
			                	{
			                		ConnectionStringName = "RavenDB"
			                	}.Initialize();

			TryCreatingIndexesOrRedirectToErrorPage();

			RavenProfiler.InitializeFor(DocumentStore,
			                            //Fields to filter out of the output
			                            "Email", "HashedPassword", "AkismetKey", "GoogleAnalyticsKey", "ShowPostEvenIfPrivate",
			                            "PasswordSalt", "UserHostAddress");
		}

		private static void TryCreatingIndexesOrRedirectToErrorPage()
		{
			try
			{
				IndexCreation.CreateIndexes(typeof (Tags_Count).Assembly, DocumentStore);
			}
			catch (WebException e)
			{
				var socketException = e.InnerException as SocketException;
				if(socketException == null)
					throw;

				switch (socketException.SocketErrorCode)
				{
					case SocketError.AddressNotAvailable:
					case SocketError.NetworkDown:
					case SocketError.NetworkUnreachable:
					case SocketError.ConnectionAborted:
					case SocketError.ConnectionReset:
					case SocketError.TimedOut:
					case SocketError.ConnectionRefused:
					case SocketError.HostDown:
					case SocketError.HostUnreachable:
					case SocketError.HostNotFound:
						HttpContext.Current.Response.Redirect("~/RavenNotReachable.htm");
						break;
					default:
						throw;
				}
			}
		}
	}
}
