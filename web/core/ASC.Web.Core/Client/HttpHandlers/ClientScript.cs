/*
 *
 * (c) Copyright Ascensio System Limited 2010-2015
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using ASC.Web.Core.Client.Templates;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Web;
using System.Web.UI;

namespace ASC.Web.Core.Client.HttpHandlers
{
    public abstract class ClientScript
    {
        protected virtual string BaseNamespace
        {
            get { return "ASC.Resources"; }
        }


        protected abstract IEnumerable<KeyValuePair<string, object>> GetClientVariables(HttpContext context);


        public string GetData(HttpContext context)
        {
            var namespaces = BaseNamespace.Split('.');
            var builder = new StringBuilder();
            var content = string.Empty;

            for (var index = 1; index <= namespaces.Length; index++)
            {
                var ns = string.Join(".", namespaces, 0, index);
                builder.AppendFormat("if (typeof({0})==='undefined'){{{0} = {{}};}} ", ns);
            }

            var store = GetClientVariables(context);
            if (store != null)
            {
                var compiler = new JqTemplateCompiler();

                foreach (var clientObject in store)
                {
                    var resourceSet = clientObject.Value as ClinetResourceSet;
                    if (resourceSet != null)
                    {
                        builder.AppendFormat("{0}.{1}={2};", BaseNamespace, clientObject.Key, JsonConvert.SerializeObject(resourceSet.GetResources()));
                        continue;
                    }

                    var templateSet = clientObject.Value as ClientTemplateSet;
                    if (templateSet != null)
                    {
                        builder.AppendFormat("{0}{1}", Environment.NewLine, templateSet.GetClientTemplates(compiler));
                        continue;
                    }

                    builder.AppendFormat("{0}.{1}={2};", BaseNamespace, clientObject.Key, JsonConvert.SerializeObject(clientObject.Value));
                }

                content = builder.ToString();
            }
            return content;
        }

        protected internal virtual string GetCacheHash()
        {
            return string.Empty;
        }


        protected KeyValuePair<string, object> RegisterObject(string key, object value)
        {
            return new KeyValuePair<string, object>(key, value);
        }

        protected KeyValuePair<string, object> RegisterResourceSet(string key, ResourceManager resourceManager)
        {
            return new KeyValuePair<string, object>(key, new ClinetResourceSet(resourceManager));
        }

        protected KeyValuePair<string, object> RegisterClientTemplatesPath(string virtualPathToControl, HttpContext context)
        {
            using (var page = new Page())
            using (var output = new StringWriter())
            {
                page.Controls.Add(page.LoadControl(virtualPathToControl));
                context.Server.Execute(page, output, false);

                var doc = new HtmlDocument();
                doc.LoadHtml(output.GetStringBuilder().ToString());
                var nodes = doc.DocumentNode.SelectNodes("/script[@type='text/x-jquery-tmpl']");
                var templates = nodes.ToDictionary(x => x.Attributes["id"].Value, y => y.InnerHtml);

                return new KeyValuePair<string, object>(Guid.NewGuid().ToString(), new ClientTemplateSet(() => templates));
            }
        }


        class ClientTemplateSet
        {
            private readonly Func<Dictionary<string, string>> getTemplates;

            public ClientTemplateSet(Func<Dictionary<string, string>> clientTemplates)
            {
                getTemplates = clientTemplates;
            }

            public string GetClientTemplates(JqTemplateCompiler compiler)
            {
                var result = new StringBuilder();

                foreach (var template in getTemplates())
                {
                    // only for jqTmpl for now
                    result.AppendFormat("jQuery.template('{0}', {1});{2}", template.Key, compiler.GetCompiledCode(template.Value), Environment.NewLine);
                }

                return result.ToString();
            }
        }

        class ClinetResourceSet
        {
            private readonly ResourceManager manager;

            public ClinetResourceSet(ResourceManager manager)
            {
                this.manager = manager;
            }

            public IDictionary<string, string> GetResources()
            {
                return manager.GetResourceSet(CultureInfo.InvariantCulture, true, true)
                    .Cast<DictionaryEntry>()
                    .Select(e => (string)e.Key)
                    .ToDictionary(k => k, k => manager.GetString(k));
            }
        }
    }
}