using System;
using System.Web;
using System.Web.Mvc;
using N2.Engine;

namespace N2.Web.Mvc
{
	/// <summary>
	/// Controller Factory class for instantiating controllers using the Windsor IoC container.
	/// </summary>
	public class N2ControllerFactory : DefaultControllerFactory
	{
		private IEngine _engine;

		/// <summary>
		/// Creates a new instance of the <see cref="N2ControllerFactory"/> class.
		/// </summary>
		/// <param name="engine">The N2 engine instance to use when creating controllers.</param>
		public N2ControllerFactory(IEngine engine)
		{
			if (engine == null)
				throw new ArgumentNullException("engine");
			_engine = engine;
		}

		public override IController CreateController(System.Web.Routing.RequestContext requestContext, string controllerName)
		{
			// TODO
			EnsureDataToken(ContentRoute.ContentItemKey, ContentRoute.ContentItemIdKey, requestContext.RouteData);
			EnsureDataToken(ContentRoute.ContentPageKey, ContentRoute.ContentPageIdKey, requestContext.RouteData);
			if(!requestContext.RouteData.DataTokens.ContainsKey(ContentRoute.ContentEngineKey))
			    requestContext.RouteData.DataTokens[ContentRoute.ContentEngineKey] = _engine;
			return base.CreateController(requestContext, controllerName);
		}

		private void EnsureDataToken(string contentKey, string idKey, System.Web.Routing.RouteData routeData)
		{
			if (!routeData.DataTokens.ContainsKey(contentKey) && routeData.Values.ContainsKey(idKey))
			{
				int id = Convert.ToInt32(routeData.Values[idKey]);
				routeData.DataTokens[contentKey] = _engine.Persister.Get(id);
			}
		}

		protected override IController GetControllerInstance(Type controllerType)
		{
			if (controllerType == null)
			{
				throw new HttpException(404, string.Format("The controller for path '{0}' could not be found or it does not implement IController.", RequestContext.HttpContext.Request.Path));
			}

			return (IController)_engine.Resolve(controllerType);
		}

		public override void ReleaseController(IController controller)
		{
			var disposable = controller as IDisposable;

			if (disposable != null)
			{
				disposable.Dispose();
			}

			_engine.Release(controller);
		}
	}
}