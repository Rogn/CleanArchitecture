using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace BlazorHero.CleanArchitecture.Infrastructure.Services.Identity
{
    public class AzureTokenService
    {
        private readonly IPublicClientApplication _app;

        private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };

        public AzureTokenService()
        {
            _app = PublicClientApplicationBuilder.Create("96720a24-8168-42ff-b8ae-864afe8ca6d3")
                .WithAuthority("https://login.microsoftonline.com/3fa4f647-1eb5-41e1-be54-29cdcc5b63e8/").Build();
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

            Microsoft.Graph.Application application = await graphServiceClient.Applications["c465140c-c3db-4f89-b1c6-b88597903b9f"].Request().Select(a => a.AppRoles).GetAsync();
            
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


            //var graphServiceClient =
            //    new GraphServiceClient(new DelegateAuthenticationProvider(async (requestMessage) =>
            //        {

            //            // Add the access token in the Authorization header of the API request.
            //            requestMessage.Headers.Authorization =
            //                new AuthenticationHeaderValue("Bearer", result.AccessToken);
            //        })
            //    );

            //var @async = graphServiceClient.Me.Request().GetAsync().Result;
            //var @async1 = graphServiceClient.Me.AppRoleAssignments.Request().GetAsync().Result;
            //var @async2 = graphServiceClient.Me.MemberOf.Request().GetAsync().Result;
        }
    }
}