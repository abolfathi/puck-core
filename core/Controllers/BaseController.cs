﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json;
using puck.core.Base;
using puck.core.Constants;
using puck.core.Helpers;
using StackExchange.Profiling;
using puck.core.State;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Localization;
using puck.core.Models;
using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace puck.core.Controllers
{
    public class BaseController : Controller
    {

        public IActionResult Puck(string variant=null)
        {
            try
            {
                StateHelper.SetFirstRequestUrl();
                SyncIfNecessary();
                var uri = Request.GetUri();
                string path = uri.AbsolutePath.ToLower().TrimEnd('/');

                var dmode = this.GetDisplayModeId();
                
                string domain = uri.Host.ToLower();
                string searchPathPrefix;
                if (!PuckCache.DomainRoots.TryGetValue(domain, out searchPathPrefix))
                {
                    if (!PuckCache.DomainRoots.TryGetValue("*", out searchPathPrefix))
                    {
                        if (PuckCache.JustSeeded) return Redirect("/puck");
                        else
                            throw new Exception($"domain root not set, likely because there is no content. DOMAIN:{domain} - visit the backoffice to set up your site");
                    }
                }
                string searchPath = searchPathPrefix.ToLower() + path;

                //do redirects
                string redirectUrl;
                if (PuckCache.Redirect301.TryGetValue(searchPath, out redirectUrl) )
                {
                    Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromMinutes(PuckCache.RedirectOuputCacheMinutes)
                    };
                    Response.Redirect(redirectUrl, true);
                }
                if (PuckCache.Redirect302.TryGetValue(searchPath, out redirectUrl))
                {
                    Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromMinutes(PuckCache.RedirectOuputCacheMinutes)
                    };
                    Response.Redirect(redirectUrl, false);
                }
                
                if (string.IsNullOrEmpty(variant))
                {
                    variant = GetVariant(searchPath);
                }
                HttpContext.Items["variant"] = variant;
                //set thread culture for future api calls on this thread
                //Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(variant);
                IList<Dictionary<string, string>> results;
#if DEBUG
                using (MiniProfiler.Current.Step("lucene"))
                {
                    results = puck.core.Helpers.QueryHelper<BaseModel>.Query(
                        string.Concat("+",FieldKeys.Published,":true"," +", FieldKeys.Path, ":", $"\"{searchPath}\"", " +", FieldKeys.Variant, ":", variant)
                        );                    
                }
#else                
                results = puck.core.Helpers.QueryHelper<BaseModel>.Query(
                        string.Concat("+",FieldKeys.Published,":true"," +", FieldKeys.Path, ":", $"\"{searchPath}\"", " +", FieldKeys.Variant, ":", variant)
                        );           
#endif
                var result = results == null ? null : results.FirstOrDefault();
                BaseModel model = null;
                if (result != null)
                {
#if DEBUG
                    using (MiniProfiler.Current.Step("deserialize"))
                    {
                        model = JsonConvert.DeserializeObject(result[FieldKeys.PuckValue], ApiHelper.GetTypeFromName(result[FieldKeys.PuckType])) as BaseModel;
                    }
#else
                    model = JsonConvert.DeserializeObject(result[FieldKeys.PuckValue], ApiHelper.GetTypeFromName(result[FieldKeys.PuckType])) as BaseModel;
#endif
                    if (!PuckCache.OutputCacheExclusion.Contains(searchPath))
                    {
                        int cacheMinutes;
                        if (!PuckCache.TypeOutputCache.TryGetValue(result[FieldKeys.PuckType], out cacheMinutes))
                        {
                            if (!PuckCache.TypeOutputCache.TryGetValue(typeof(BaseModel).Name, out cacheMinutes))
                            {
                                cacheMinutes = PuckCache.DefaultOutputCacheMinutes;
                            }
                        }
                        Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                        {
                            Public = true,
                            MaxAge = TimeSpan.FromMinutes(cacheMinutes)
                        };
                    }
                }

                if (model == null)
                {
                    //404
                    HttpContext.Response.StatusCode = 404;
                    return View(PuckCache.Path404);
                }
                var cache = PuckCache.Cache;
                object cacheValue;
                //string templatePath = result[FieldKeys.TemplatePath];
                string templatePath = model.TemplatePath;
                
                if (!string.IsNullOrEmpty(dmode))
                {
                    string cacheKey = CacheKeys.PrefixTemplateExist + dmode + templatePath;
                    if (cache.TryGetValue(cacheKey,out cacheValue))
                    {
                        templatePath = cacheValue as string;
                    }
                    else
                    {
                        string dpath = templatePath.Insert(templatePath.LastIndexOf('.') + 1, dmode + ".");
                        if (System.IO.File.Exists(ApiHelper.MapPath(dpath)))
                        {
                            templatePath = dpath;
                        }
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromMinutes(PuckCache.DisplayModesCacheMinutes));
                        cache.Set(cacheKey, templatePath, cacheEntryOptions);
                    }
                }
                if (templatePath.ToLower().Equals(PuckCache.Path404?.ToLower() ?? "")) {
                    HttpContext.Response.StatusCode = 404;
                }
                return View(templatePath, model);
            }
            catch (Exception ex)
            {
                PuckCache.PuckLog.Log(ex);
                return ErrorPage(ex);
            }
        }

        public string GetVariant(string searchPath)
        {
            string variant = null;
            if (!PuckCache.PathToLocale.TryGetValue(searchPath, out variant))
            {
                foreach (var entry in PuckCache.PathToLocale)
                {//PathToLocale dictionary ordered by depth descending (based on number of forward slashes in path) so it's safe to break after first match
                    if ((searchPath+"/").StartsWith(entry.Key+"/"))
                    {
                        variant = entry.Value;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(variant))
                    variant = PuckCache.SystemVariant;
            }
            return variant;
        }

        public string GetDisplayModeId() {
            var dmode = "";
            if (PuckCache.DisplayModes != null)
            {
                foreach (var mode in PuckCache.DisplayModes)
                {
                    if (mode.Value(HttpContext))
                    {
                        dmode = mode.Key;
                        break;
                    }
                }
            }
            return dmode;
        }

        protected void SyncIfNecessary() {
            if (PuckCache.ShouldSync && !PuckCache.IsSyncQueued)
            {
                PuckCache.IsSyncQueued = true;
                //was using HostingEnvironment.QueueBackgroundWorkItem and passing in cancellation token
                //can't do that in asp.net core so passing in a new cancellation token which is a bit pointless
                System.Threading.Tasks.Task.Factory.StartNew(() => SyncHelper.Sync(new CancellationToken()));
            }
        }

        protected IActionResult ErrorPage(Exception exception=null) {
            HttpContext.Response.StatusCode = 500;
            var model = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
            if (exception == null)
            {
                var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
                if (exceptionHandlerFeature != null)
                    model.Exception = exceptionHandlerFeature.Error;
            }
            else
                model.Exception = exception;
            return View(PuckCache.Path500,model);
        }

    }
}
