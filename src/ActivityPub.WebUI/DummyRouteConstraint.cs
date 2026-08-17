using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ActivityPub.WebUI;

public class DummyRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        return true;
    }
}
