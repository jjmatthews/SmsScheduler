﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using NServiceBus;
using SmsWeb.Controllers;

namespace SmsWeb
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            AuthConfig.RegisterAuth();


            Configure.With()
            .DefaultBuilder()
                .Log4Net()
            .XmlSerializer()
            .MsmqTransport()
                .IsTransactional(true)
                .PurgeOnStartup(false)
            .UnicastBus()
                .LoadMessageHandlers()
            .CreateBus()
            .Start(() => Configure.Instance.ForInstallationOn<NServiceBus.Installation.Environments.Windows>().Install());

            RegisterControllers();
            ControllerBuilder.Current.SetControllerFactory(new IoCControllerFactory());
        }

        private void RegisterControllers()
        {
            var allTypes = typeof(HomeController).Assembly.GetTypes();
            var controllers = allTypes.Where(t => t.IsSubclassOf(typeof(Controller)));
            foreach (var controller in controllers)
            {
                Configure.Instance.Configurer.ConfigureComponent(controller, DependencyLifecycle.InstancePerCall);
            }
        }
    }


    public class IoCControllerFactory : DefaultControllerFactory
    {
        protected override IController GetControllerInstance(System.Web.Routing.RequestContext requestContext, Type controllerType)
        {
            try
            {
                if (controllerType == null) return null;
                var controller = Configure.Instance.Builder.Build(controllerType) as Controller;

                // HACK: IoCControllerFactory: This property setting is ugly. it was put in for the EftTokeniserController.HttpContextBase property. NEED to determine if we need this and if not remove it!!!! Possibly use the HttpContext property instead (if we can work out how to inject a HttpContextBase at test time).
                if (controller == null) return null;
                var setProperties = controller.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(HttpContextBase))
                    .Select(p => p.GetSetMethod())
                    .Where(p => p != null);
                foreach (var setProperty in setProperties)
                {
                    setProperty.Invoke(controller, new[] { requestContext.HttpContext });
                }

                return controller;
            }
            catch (Exception e)
            {
                var typeName = controllerType != null ? controllerType.FullName : "No name specified";
                var message = string.Format("Container Missing reference to a controller of type '{0}'.", typeName);
                Debug.Write(message);
                throw new ArgumentException(message, e);
            }
        }
    }
}