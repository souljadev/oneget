// 
//  Copyright (c) Microsoft Corporation. All rights reserved. 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  

namespace Microsoft.OneGet.Implementation {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Api;
    using Packaging;
    using Providers;
    using Utility.Async;
    using Utility.Collections;
    using Utility.Plugin;
    using IRequestObject = System.Object;

    public class PackageProvider : ProviderBase<IPackageProvider> {
        private string _name;

        internal PackageProvider(IPackageProvider provider) : base(provider) {
        }

        public string Name {
            get {
                return ProviderName;
            }
        }

        public override string ProviderName {
            get {
                return _name ?? (_name = Provider.GetPackageProviderName());
            }
        }

        // Friendly APIs

        public IAsyncEnumerable<PackageSource> AddPackageSource(string name, string location, bool trusted, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();
            return new PackageSourceRequestObject(this, requestObject.As<IHostApi>(), request => Provider.AddPackageSource(name, location, trusted, request));
        }

        public IAsyncEnumerable<PackageSource> RemovePackageSource(string name, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();
            return new PackageSourceRequestObject(this, requestObject.As<IHostApi>(), request => Provider.RemovePackageSource(name, request));
        }

        public IAsyncEnumerable<SoftwareIdentity> FindPackageByUri(Uri uri, int id, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            if (!IsSupportedScheme(uri)) {
                return new EmptyAsyncEnumerable<SoftwareIdentity>();
            }

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.FindPackageByUri(uri, id, request), Constants.PackageStatus.Available);
        }

        public IAsyncEnumerable<SoftwareIdentity> GetPackageDependencies(SoftwareIdentity package, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.GetPackageDependencies(package.FastPackageReference, request), Constants.PackageStatus.Dependency);
        }

        public IAsyncEnumerable<SoftwareIdentity> FindPackageByFile(string filename, int id, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            if (!IsSupportedFile(filename)) {
                return new EmptyAsyncEnumerable<SoftwareIdentity>();
            }

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.FindPackageByFile(filename, id, request), Constants.PackageStatus.Available);
        }

        public IAsyncValue<int> StartFind(IRequestObject requestObject) {
            if (requestObject == null) {
                throw new ArgumentNullException("requestObject");
            }

            return new FuncRequestObject<int>(this, requestObject.As<IHostApi>(), request => Provider.StartFind(request));
        }

        public IAsyncEnumerable<SoftwareIdentity> CompleteFind(int i, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.CompleteFind(i, request), "Available");
        }

        public IAsyncEnumerable<SoftwareIdentity> FindPackages(string[] names, string requiredVersion, string minimumVersion, string maximumVersion, IRequestObject requestObject) {
            if (requestObject == null) {
                throw new ArgumentNullException("requestObject");
            }

            if (names == null) {
                throw new ArgumentNullException("names");
            }

            if (names.Length == 0) {
                return FindPackage(null, requiredVersion, minimumVersion, maximumVersion, 0, requestObject);
            }

            if (names.Length == 1) {
                return FindPackage(names[0], requiredVersion, minimumVersion, maximumVersion, 0, requestObject);
            }

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => {
                var id = StartFind(request);
                foreach (var name in names) {
                    Provider.FindPackage(name, requiredVersion, minimumVersion, maximumVersion, id.Value, request);
                }
                Provider.CompleteFind(id.Value, request);
            }, Constants.PackageStatus.Available);
        }

        /*
        private IEnumerable<SoftwareIdentity> FindPackagesImpl(CancellationTokenSource cancellationTokenSource, string[] names, string requiredVersion, string minimumVersion, string maximumVersion, IRequestObject requestObject) {
            var id = StartFind(requestObject);
            foreach (var name in names) {
                foreach (var pkg in FindPackage(name, requiredVersion, minimumVersion, maximumVersion, id, requestObject).TakeWhile(pkg => !cancellationTokenSource.IsCancellationRequested)) {
                    yield return pkg;
                }
                foreach (var pkg in CompleteFind(id, requestObject).TakeWhile(pkg => !cancellationTokenSource.IsCancellationRequested)) {
                    yield return pkg;
                }
            }
        }
*/

        public IAsyncEnumerable<SoftwareIdentity> FindPackagesByUris(Uri[] uris, IRequestObject requestObject) {
            if (requestObject == null) {
                throw new ArgumentNullException("requestObject");
            }

            if (uris == null) {
                throw new ArgumentNullException("uris");
            }

            if (uris.Length == 0) {
                return new EmptyAsyncEnumerable<SoftwareIdentity>();
            }

            if (uris.Length == 1) {
                return FindPackageByUri(uris[0], 0, requestObject);
            }

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => {
                var id = StartFind(request);
                foreach (var uri in uris) {
                    Provider.FindPackageByUri(uri, id.Value, request);
                }
                Provider.CompleteFind(id.Value, request);
            }, Constants.PackageStatus.Available);
        }

        /*
        private IEnumerable<SoftwareIdentity> FindPackagesByUrisImpl(CancellationTokenSource cancellationTokenSource, Uri[] uris, IRequestObject requestObject) {
            var id = StartFind(requestObject);
            foreach (var uri in uris) {
                foreach (var pkg in FindPackageByUri(uri, id.Value, requestObject).TakeWhile(pkg => !cancellationTokenSource.IsCancellationRequested)) {
                    yield return pkg;
                }
                foreach (var pkg in CompleteFind(id.Value, requestObject).TakeWhile(pkg => !cancellationTokenSource.IsCancellationRequested)) {
                    yield return pkg;
                }
            }
        }
*/

        public IAsyncEnumerable<SoftwareIdentity> FindPackagesByFiles(string[] filenames, IRequestObject requestObject) {
            if (requestObject == null) {
                throw new ArgumentNullException("requestObject");
            }

            if (filenames == null) {
                throw new ArgumentNullException("filenames");
            }

            if (filenames.Length == 0) {
                return new EmptyAsyncEnumerable<SoftwareIdentity>();
            }

            if (filenames.Length == 1) {
                return FindPackageByFile(filenames[0], 0, requestObject);
            }

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => {
                var id = StartFind(request);
                foreach (var file in filenames) {
                    Provider.FindPackageByFile(file, id.Value, request);
                }
                Provider.CompleteFind(id.Value, request);
            }, Constants.PackageStatus.Available);
        }

        private IEnumerable<SoftwareIdentity> FindPackagesByFilesImpl(CancellationTokenSource cancellationTokenSource, string[] filenames, IRequestObject requestObject) {
            var id = StartFind(requestObject);
            foreach (var file in filenames) {
                foreach (var pkg in FindPackageByFile(file, id.Value, requestObject).TakeWhile(pkg => !cancellationTokenSource.IsCancellationRequested)) {
                    yield return pkg;
                }
                foreach (var pkg in CompleteFind(id.Value, requestObject).TakeWhile(pkg => !cancellationTokenSource.IsCancellationRequested)) {
                    yield return pkg;
                }
            }
        }

        public IAsyncEnumerable<SoftwareIdentity> FindPackage(string name, string requiredVersion, string minimumVersion, string maximumVersion, int id, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.FindPackage(name, requiredVersion, minimumVersion, maximumVersion, id, request), Constants.PackageStatus.Available);
        }

        public IAsyncEnumerable<SoftwareIdentity> GetInstalledPackages(string name, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.GetInstalledPackages(name, request), Constants.PackageStatus.Installed);
        }

        public IAsyncEnumerable<SoftwareIdentity> InstallPackage(SoftwareIdentity softwareIdentity, IRequestObject requestObject) {
            if (requestObject == null) {
                throw new ArgumentNullException("requestObject");
            }

            if (softwareIdentity == null) {
                throw new ArgumentNullException("softwareIdentity");
            }
            var hostApi = requestObject.As<IHostApi>();

            // if the provider didn't say this was trusted, we should ask the user if it's ok.
            if (!softwareIdentity.FromTrustedSource) {
                try {
                    if (!hostApi.ShouldContinueWithUntrustedPackageSource(softwareIdentity.Name, softwareIdentity.Source)) {
                        hostApi.Warning(hostApi.FormatMessageString(Constants.Messages.UserDeclinedUntrustedPackageInstall, softwareIdentity.Name));
                        return new EmptyAsyncEnumerable<SoftwareIdentity>();
                    }
                } catch {
                    return new EmptyAsyncEnumerable<SoftwareIdentity>();
                }
            }

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.InstallPackage(softwareIdentity.FastPackageReference, request), Constants.PackageStatus.Installed);
        }

        public IAsyncEnumerable<SoftwareIdentity> UninstallPackage(SoftwareIdentity softwareIdentity, IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            return new SoftwareIdentityRequestObject(this, requestObject.As<IHostApi>(), request => Provider.UninstallPackage(softwareIdentity.FastPackageReference, request), Constants.PackageStatus.Uninstalled);
        }

        public IAsyncEnumerable<PackageSource> ResolvePackageSources(IRequestObject requestObject) {
            requestObject = requestObject ?? new object();

            return new PackageSourceRequestObject(this, requestObject.As<IHostApi>(), request => Provider.ResolvePackageSources(request));
        }

        public IAsyncAction DownloadPackage(SoftwareIdentity softwareIdentity, string destinationFilename, IRequestObject requestObject) {
            if (requestObject == null) {
                throw new ArgumentNullException("requestObject");
            }

            if (softwareIdentity == null) {
                throw new ArgumentNullException("softwareIdentity");
            }

            return new ActionRequestObject(this, requestObject.As<IHostApi>(), request => Provider.DownloadPackage(softwareIdentity.FastPackageReference, destinationFilename, request));
        }

        internal void ExecuteElevatedAction(string payload, IRequestObject requestObject) {
            new ActionRequestObject(this, requestObject.As<IHostApi>(), request => Provider.ExecuteElevatedAction(payload, request)).Wait();
        }
    }
}