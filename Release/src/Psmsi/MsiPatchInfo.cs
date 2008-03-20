// Represents installed patches.
//
// Author: Heath Stewart <heaths@microsoft.com>
// Created: Thu, 01 Feb 2007 18:39:11 GMT
//
// Copyright (C) Microsoft Corporation. All rights reserved.
//
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
// KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;
using System.Resources;
using System.Text;
using Microsoft.Windows.Installer.PowerShell;
using Microsoft.Windows.Installer.Properties;
using Microsoft.Windows.Installer.PowerShell.Commands;

namespace Microsoft.Windows.Installer
{
    public class PatchInfo
    {
        string patchCode, productCode, userSid;
        InstallContext context;

        internal PatchInfo(string patchCode, string productCode, string userSid, InstallContext context)
        {
            Debug.Assert(!string.IsNullOrEmpty(patchCode));
            Debug.Assert(!string.IsNullOrEmpty(productCode));

            // Validate InstallContext and UserSid combinations.
            if (((InstallContext.UserManaged | InstallContext.UserUnmanaged) & context) != 0
                && string.IsNullOrEmpty(userSid))
            {
                throw new PSArgumentException(Resources.Argument_InvalidContextAndSid);
            }

            this.patchCode = patchCode;
            this.productCode = string.IsNullOrEmpty(productCode) ? null : productCode;
            this.userSid = string.IsNullOrEmpty(userSid) ||
                context == InstallContext.Machine ? null : userSid;
            this.context = context;
        }

        public string PatchCode { get { return patchCode; } }
        public string ProductCode { get { return productCode; } }
        public string UserSid { get { return userSid; } }
        public InstallContext InstallContext { get { return context; } }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Uninstallable")]
        public bool Uninstallable
        {
            get
            {
                return (bool)GetProperty<bool>(NativeMethods.INSTALLPROPERTY_UNINSTALLABLE, ref uninstallable);
            }
        }
        string uninstallable;

        public PatchStates PatchState
        {
            get
            {
                return (PatchStates)GetProperty<PatchStates>(NativeMethods.INSTALLPROPERTY_PATCHSTATE, ref patchState);
            }
        }
        string patchState;

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "LUA")]
        public bool LUAEnabled
        {
            get
            {
                return (bool)GetProperty<bool>(NativeMethods.INSTALLPROPERTY_LUAENABLED, ref luaEnabled);
            }
        }
        string luaEnabled;

        public string DisplayName
        {
            get
            {
                return (string)GetProperty<string>(NativeMethods.INSTALLPROPERTY_DISPLAYNAME, ref displayName);
            }
        }
        string displayName;

        [SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings")]
        public string MoreInfoUrl
        {
            get
            {
                return (string)GetProperty<string>(NativeMethods.INSTALLPROPERTY_MOREINFOURL, ref moreInfoUrl);
            }
        }
        string moreInfoUrl;

        public string LocalPackage
        {
            get
            {
                return (string)GetProperty<string>(NativeMethods.INSTALLPROPERTY_LOCALPACKAGE, ref localPackage);
            }
        }
        string localPackage;

        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "1#"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
        protected object GetProperty<T>(string property, ref string field)
        {
            // If field is not yet assigned, get product property.
            if (string.IsNullOrEmpty(field))
            {
                Debug.Assert(!string.IsNullOrEmpty(property));
                field = GetPatchProperty(property);
            }

            // Based on type T, convert non-null or empty string to T.
            if (!string.IsNullOrEmpty(field))
            {
                Type t = typeof(T);
                if (t == typeof(bool))
                {
                    return string.CompareOrdinal(field.Trim(), "0") != 0;
                }
                else if (t == typeof(DateTime))
                {
                    // Dates in yyyyMMdd format.
                    return DateTime.ParseExact(field, "yyyyMMdd", null);
                }
                else
                {
                    //Everything else, use a TypeConverter.
                    TypeConverter converter = TypeDescriptor.GetConverter(t);
                    return converter.ConvertFromString(field);
                }
            }

            return default(T);
        }

        string GetPatchProperty(string property)
        {
            int ret = 0;
            StringBuilder sb = new StringBuilder(80);
            int cch = sb.Capacity;

            // Use older MsiGetPatchInfo if no ProductCode is specified.
            if (string.IsNullOrEmpty(productCode) || Msi.CheckVersion(3, 0))
            {
                // Use MsiGetPatchInfoEx for MSI versions 3.0 and newer.
                ret = NativeMethods.MsiGetPatchInfoEx(patchCode, productCode, userSid, context, property, sb, ref cch);
                if (NativeMethods.ERROR_MORE_DATA == ret)
                {
                    sb.Capacity = ++cch;
                    ret = NativeMethods.MsiGetPatchInfoEx(patchCode, productCode, userSid, context, property, sb, ref cch);
                }
            }
            else
            {
                // Use MsiGetPatchInfo for MSI versions prior to 3.0 or if no ProductCode is specified.
                ret = NativeMethods.MsiGetPatchInfo(patchCode, property, sb, ref cch);
                if (NativeMethods.ERROR_MORE_DATA == ret)
                {
                    sb.Capacity = ++cch;
                    ret = NativeMethods.MsiGetPatchInfo(patchCode, property, sb, ref cch);
                }
            }

            if (NativeMethods.ERROR_SUCCESS == ret)
            {
                return sb.ToString();
            }

            // Getting this far means an unexpected error occured.
            throw new Win32Exception(ret);
        }
    }
}
