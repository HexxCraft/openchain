﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace OpenChain.Ledger
{
    public class DefaultPermissionLayout : IPermissionsProvider
    {
        private readonly IList<PathPermissions> permissions;
        private readonly bool allowThirdPartyAssets;
        private readonly byte versionByte;
        private readonly LedgerPath assetPath = LedgerPath.Parse("/asset/");
        private readonly LedgerPath thirdPartyAssetPath = LedgerPath.Parse("/asset/p2pkh/");
        private readonly LedgerPath p2pkhAccountPath = LedgerPath.Parse("/p2pkh/");

        public DefaultPermissionLayout(IList<PathPermissions> permissions, bool allowThirdPartyAssets, byte versionByte)
        {
            this.permissions = permissions;
            this.allowThirdPartyAssets = allowThirdPartyAssets;
            this.versionByte = versionByte;
        }

        public Task<PermissionSet> GetPermissions(IReadOnlyList<SignatureEvidence> authentication, LedgerPath path)
        {
            IReadOnlyList<string> identities = authentication.Select(evidence => GetPubKeyHash(evidence.PublicKey)).ToList().AsReadOnly();

            bool isAccountPath = p2pkhAccountPath.IsStrictParentOf(path);
            bool isAssetPath = assetPath.IsStrictParentOf(path);

            foreach (PathPermissions permission in permissions)
            {
                if (permission.Path.IsParentOf(path) && identities.Any(identity => permission.Identities.Contains(identity, StringComparer.Ordinal)))
                    return Task.FromResult(permission.Permissions);
            }

            bool issuancePath = this.allowThirdPartyAssets
                    && thirdPartyAssetPath.IsStrictParentOf(path)
                    && identities.Contains(path.Segments[2]);

            return Task.FromResult(new PermissionSet(
                issuance: issuancePath,
                spendFrom: isAccountPath && identities.Contains(path.Segments[1]),
                affectBalance: (isAccountPath && IsP2pkh(path.Segments[1])) || issuancePath,
                modifyData: issuancePath));
        }

        private bool IsP2pkh(string address)
        {
            try
            {
                byte[] pubKeyHash = Base58CheckEncoding.Decode(address);
                if (pubKeyHash.Length != 21 || pubKeyHash[0] != versionByte)
                    return false;
                else
                    return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private string GetPubKeyHash(BinaryData pubKey)
        {
            using (RIPEMD160 ripe = RIPEMD160.Create())
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] result = ripe.ComputeHash(sha256.ComputeHash(pubKey.ToByteArray()));
                return Base58CheckEncoding.Encode(new byte[] { versionByte }.Concat(result).ToArray());
            }
        }
    }
}
