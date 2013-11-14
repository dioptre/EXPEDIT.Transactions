using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Mvc.Routes;

namespace EXPEDIT.Transactions
{
    public class Routes : IRouteProvider
    {
        public void GetRoutes(ICollection<RouteDescriptor> routes)
        {
            foreach (var routeDescriptor in GetRoutes())
                routes.Add(routeDescriptor);
        }

        public IEnumerable<RouteDescriptor> GetRoutes()
        {
            return new[] {
                new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Transactions/{controller}/{action}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},
                            {"action", "Index"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Transactions/{controller}/{action}/{id}/{verb}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Transactions/{controller}/{action}/{id}/{ref}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                },
                new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Store/{controller}/{action}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},
                            {"action", "Index"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Store/{controller}/{action}/{id}/{verb}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                },
                new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Products/{controller}/{action}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},
                            {"action", "Index"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Products/{controller}/{action}/{id}/{verb}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Transactions"}
                        },
                        new MvcRouteHandler())
                }

            };
        }
    }
}