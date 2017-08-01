﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Lib.ioc;
using Lib.helper;
using Lib.core;
using Lib.extension;
using Lib.mvc.user;
using System.Net;
using System.Net.Http;
using Lib.net;

namespace Lib.mvc.auth.validation
{
    public abstract class TokenValidationProviderBase
    {
        public abstract LoginUserInfo FindUser(HttpContext context);

        public virtual async Task<LoginUserInfo> FindUserAsync(HttpContext context) => await Task.FromResult(this.FindUser(context));
    }

    /// <summary>
    /// 直接使用login status
    /// </summary>
    public class CookieTokenValidationProvider : TokenValidationProviderBase
    {
        private readonly LoginStatus _LoginStatus;

        public CookieTokenValidationProvider(LoginStatus _LoginStatus)
        {
            this._LoginStatus = _LoginStatus;
        }

        public override LoginUserInfo FindUser(HttpContext context)
        {
            return this._LoginStatus.GetLoginUser(context);
        }
    }

    /// <summary>
    /// 查询本地库
    /// </summary>
    public class AuthLocalValidationProvider : TokenValidationProviderBase
    {
        private readonly IValidationDataProvider _dataProvider;

        public AuthLocalValidationProvider(IValidationDataProvider _dataProvider)
        {
            this._dataProvider = _dataProvider;
        }

        public override LoginUserInfo FindUser(HttpContext context)
        {
            try
            {
                var token = this._dataProvider.GetToken(context);
                var client_id = this._dataProvider.GetClientID(context);
                if (!ValidateHelper.IsAllPlumpString(token, client_id))
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                e.AddErrorLog();
                return null;
            }
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 请求auth server验证
    /// </summary>
    public class AuthServerValidationProvider : TokenValidationProviderBase
    {
        private readonly AuthServerConfig _server;
        private readonly IValidationDataProvider _dataProvider;

        public AuthServerValidationProvider(AuthServerConfig server, IValidationDataProvider _dataProvider)
        {
            this._server = server;
            this._dataProvider = _dataProvider;
        }

        public override LoginUserInfo FindUser(HttpContext context)
        {
            try
            {
                var token = this._dataProvider.GetToken(context);
                var client_id = this._dataProvider.GetClientID(context);
                if (!ValidateHelper.IsAllPlumpString(token, client_id))
                {
                    return null;
                }

                var json = HttpClientHelper.Post_(this._server.CheckToken(), new
                {
                    client_id = client_id,
                    access_token = token
                });
                var data = json.JsonToEntity<_<LoginUserInfo>>();
                if (!data.success)
                {
                    $"check token返回数据:{data.ToJson()}".AddBusinessInfoLog();
                    return null;
                }
                return data.data;
            }
            catch (Exception e)
            {
                e.AddErrorLog();
                return null;
            }
        }

        public override async Task<LoginUserInfo> FindUserAsync(HttpContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var client_id = context.Request.Headers["client_id"];
                if (!ValidateHelper.IsAllPlumpString(token, client_id))
                {
                    return null;
                }

                var client = HttpClientManager.Instance.DefaultClient;
                var response = await client.PostAsJsonAsync(this._server.CheckToken(), new
                {
                    client_id = client_id,
                    access_token = token
                });

                using (response)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = json.JsonToEntity<_<LoginUserInfo>>();
                    if (!data.success)
                    {
                        $"check token返回数据:{data.ToJson()}".AddBusinessInfoLog();
                        return null;
                    }
                    return data.data;
                }
            }
            catch (Exception e)
            {
                e.AddErrorLog();
                return null;
            }
        }
    }
}
