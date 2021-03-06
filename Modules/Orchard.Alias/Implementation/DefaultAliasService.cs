using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Orchard.Alias.Implementation.Storage;
using Orchard.Mvc.Routes;
using Orchard.Utility.Extensions;

namespace Orchard.Alias.Implementation {
    public class DefaultAliasService : IAliasService {
        private readonly IAliasStorage _aliasStorage;
        private readonly IEnumerable<IRouteProvider> _routeProviders;
        private readonly Lazy<IEnumerable<RouteDescriptor>> _routeDescriptors;

        public DefaultAliasService(
            IAliasStorage aliasStorage,
            IEnumerable<IRouteProvider> routeProviders) {
            _aliasStorage = aliasStorage;
            _routeProviders = routeProviders;

            _routeDescriptors = new Lazy<IEnumerable<RouteDescriptor>>(GetRouteDescriptors);
        }

        public RouteValueDictionary Get(string aliasPath) {
            return _aliasStorage.Get(aliasPath).ToRouteValueDictionary();
        }

        public void Set(string aliasPath, RouteValueDictionary routeValues, string aliasSource) {
            _aliasStorage.Set(
                aliasPath,
                ToDictionary(routeValues),
                aliasSource);
        }

        public void Set(string aliasPath, string routePath, string aliasSource) {
            _aliasStorage.Set(
                aliasPath.Trim('/'),
                ToDictionary(routePath),
                aliasSource);
        }

        public void Delete(string aliasPath) {
            
            if(aliasPath == null) {
                aliasPath = String.Empty;
            }

            _aliasStorage.Remove(aliasPath);
        }
        public void DeleteBySource(string aliasSource) {
            _aliasStorage.RemoveBySource(aliasSource);
        }

        public IEnumerable<string> Lookup(string routePath) {
            return Lookup(ToDictionary(routePath).ToRouteValueDictionary());
        }

        public void Replace(string aliasPath, RouteValueDictionary routeValues, string aliasSource) {
            foreach (var lookup in Lookup(routeValues).Skip(1)) {
                Delete(lookup);
            }
            Set(aliasPath, routeValues, aliasSource);
        }

        public void Replace(string aliasPath, string routePath, string aliasSource) {
            Replace(aliasPath, ToDictionary(routePath).ToRouteValueDictionary(), aliasSource);
        }

        public IEnumerable<string> Lookup(RouteValueDictionary routeValues) {
            return List().Where(item => item.Item2.Match(routeValues)).Select(item=>item.Item1).ToList();
        }
        
        public IEnumerable<Tuple<string, RouteValueDictionary>> List() {
            return _aliasStorage.List().Select(item => Tuple.Create(item.Item1, item.Item3.ToRouteValueDictionary()));
        }

        public IEnumerable<Tuple<string, RouteValueDictionary, string>> List(string sourceStartsWith) {
            return _aliasStorage.List(sourceStartsWith).Select(item => Tuple.Create(item.Item1, item.Item3.ToRouteValueDictionary(), item.Item4));
        }

        public IEnumerable<VirtualPathData> LookupVirtualPaths(RouteValueDictionary routeValues,HttpContextBase httpContext) {
            return Utils.LookupVirtualPaths(httpContext, _routeDescriptors.Value, routeValues);
        }

        private IDictionary<string, string> ToDictionary(string routePath) {
            if (routePath == null)
                return null;

            return Utils.LookupRouteValues(new StubHttpContext(), _routeDescriptors.Value, routePath);
        }

        private static IDictionary<string, string> ToDictionary(IEnumerable<KeyValuePair<string, object>> routeValues) {
            if (routeValues == null)
                return null;

            return routeValues.ToDictionary(kv => kv.Key, kv => Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
        }

        private IEnumerable<RouteDescriptor> GetRouteDescriptors() {
            return _routeProviders
                .SelectMany(routeProvider => {
                                var routes = new List<RouteDescriptor>();
                                routeProvider.GetRoutes(routes);
                                return routes;
                            })
                .Where(routeDescriptor => !(routeDescriptor.Route is AliasRoute))
                .OrderByDescending(routeDescriptor => routeDescriptor.Priority);
        }

        private class StubHttpContext : HttpContextBase {
            public override HttpRequestBase Request
            {
                get{return new StubHttpRequest();}
            }

            private class StubHttpRequest : HttpRequestBase {}
        }
    }
}