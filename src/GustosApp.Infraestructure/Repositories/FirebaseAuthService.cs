using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin.Auth;
using GustosApp.Application.Interfaces;
namespace GustosApp.Infraestructure.Repositories
{



    public class FirebaseAuthService : IFirebaseAuthService
    {
        public async Task SetUserRoleAsync(string firebaseUid, string rol)
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await FirebaseAuth.DefaultInstance
                .SetCustomUserClaimsAsync(firebaseUid, new Dictionary<string, object>
                {
                { "rol", rol }
                }, limite.Token);
        }

        // Este metodo lo creo par testear el caso de uso AppLoginConFirebaseUseCaseTest
        public async Task UpdateUserEmailAsync(string firebaseUid, string newEmail)
        {
            var args = new UserRecordArgs()
            {
                Uid = firebaseUid,
                Email = newEmail
            };

            await FirebaseAuth.DefaultInstance.UpdateUserAsync(args);
        }

        public async Task<DecodedToken> VerifyIdTokenAsync(
            string idToken,
            bool comprobarRevocacion = false,
            CancellationToken ct = default)
        {
            var firebaseTokenReal = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(
                idToken,
                comprobarRevocacion,
                ct);

            return new DecodedToken
            {
                Uid = firebaseTokenReal.Uid,
                Claims = firebaseTokenReal.Claims.ToDictionary(claim => claim.Key, claim => claim.Value)
            };
        }

        public async Task<FirebaseUserInfo?> GetUserByUidAsync(string firebaseUid)
        {
            try
            {
                var userRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(firebaseUid);
                
                return new FirebaseUserInfo
                {
                    Uid = userRecord.Uid,
                    Email = userRecord.Email,
                    DisplayName = userRecord.DisplayName,
                    PhotoUrl = userRecord.PhotoUrl
                };
            }
            catch (FirebaseAuthException)
            {
                return null;
            }
        }
    
    }

}
