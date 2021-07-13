using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security;
using System.Threading.Tasks;
using BlazorHero.CleanArchitecture.Application.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace BlazorHero.CleanArchitecture.Infrastructure.Services.Identity
{
    public class AzureTokenService
    {
        private readonly IOptions<AppConfiguration> _appConfig;
        private readonly IPublicClientApplication _app;

        private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };

        public AzureTokenService(IOptions<AppConfiguration> appConfig)
        {
            _appConfig = appConfig;
            _app = PublicClientApplicationBuilder.Create(_appConfig.Value.ClientId)
                .WithAuthority(_appConfig.Value.Authority).Build();
        }

        public async Task<User> GetUser(string userName, string password)
        {
            var graphServiceClient =
                new GraphServiceClient(new DelegateAuthenticationProvider(async requestMessage =>
                    {
                        AuthenticationResult result = await GetToken(userName, password);
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                    })
                );

            return await graphServiceClient.Me.Request().Expand(r => r.AppRoleAssignments).GetAsync();
        }

        public async Task<List<string>> GetRoles(string userName, string password)
        {
            var graphServiceClient =
                new GraphServiceClient(new DelegateAuthenticationProvider(async requestMessage =>
                    {
                        AuthenticationResult result = await GetToken(userName, password);
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                    })
                );

            var userAppRoleAssignments = await graphServiceClient.Me.AppRoleAssignments.Request().GetAsync();

            Microsoft.Graph.Application application = await graphServiceClient.Applications[_appConfig.Value.ApplicationObjectId].Request().Select(a => a.AppRoles).GetAsync();
            
            return application.AppRoles.Where(a => userAppRoleAssignments
                .Select(u => u.AppRoleId).Contains(a.Id))
                .Select(a => a.Value).ToList();
        }

        private async Task<AuthenticationResult> GetToken(string userName, string password)
        {
            IAccount account = (await _app.GetAccountsAsync()).SingleOrDefault(a => a.Username == userName);
            if (account != null)
            {
                return await _app.AcquireTokenSilent(_scopes, account)
                    .WithForceRefresh(true)
                    .ExecuteAsync();
                
            }

            var securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }

            AuthenticationResult result = await _app
                .AcquireTokenByUsernamePassword(_scopes, userName, securePassword)
                .ExecuteAsync();

            return result;
        }
    }
}