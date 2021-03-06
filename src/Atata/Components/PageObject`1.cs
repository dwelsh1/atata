﻿using System;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;

namespace Atata
{
    /// <summary>
    /// Represents the base class for the page objects.
    /// </summary>
    /// <typeparam name="TOwner">The type of the owner page object.</typeparam>
    public abstract class PageObject<TOwner> : UIComponent<TOwner>, IPageObject<TOwner>, IPageObject
        where TOwner : PageObject<TOwner>
    {
        protected PageObject()
        {
            NavigateOnInit = true;
            ScopeLocator = new PlainScopeLocator(CreateScopeBy);

            ScopeSource = ScopeSource.PageObject;
            Owner = (TOwner)this;
        }

        protected internal bool NavigateOnInit { get; internal set; }

        protected bool IsTemporarilyNavigated { get; private set; }

        protected UIComponent PreviousPageObject { get; private set; }

        /// <summary>
        /// Gets the <see cref="DataProvider{TData, TOwner}"/> instance for the title of the current HTML page.
        /// </summary>
        public DataProvider<string, TOwner> PageTitle => GetOrCreateDataProvider("title", GetTitle);

        /// <summary>
        /// Gets the <see cref="DataProvider{TData, TOwner}"/> instance for the URL of the current HTML page.
        /// </summary>
        public DataProvider<string, TOwner> PageUrl => GetOrCreateDataProvider("URL", GetUrl);

        protected virtual By CreateScopeBy()
        {
            string scopeXPath = Metadata.ComponentDefinitonAttribute?.ScopeXPath ?? "body";

            return By.XPath($".//{scopeXPath}");
        }

        private string GetTitle()
        {
            return Driver.Title;
        }

        private string GetUrl()
        {
            return Driver.Url;
        }

        internal void Init()
        {
            Log = AtataContext.Current.Log;
            Driver = AtataContext.Current.Driver;

            ComponentName = UIComponentResolver.ResolvePageObjectName<TOwner>();
            ComponentTypeName = UIComponentResolver.ResolvePageObjectTypeName<TOwner>();

            Log.Info("Go to {0}", ComponentFullName);

            OnInit();

            if (NavigateOnInit)
                Navigate();

            InitComponent();

            ExecuteTriggers(TriggerEvents.Init);

            OnVerify();
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void Navigate()
        {
            UrlAttribute attribute;

            if (GetType().TryGetCustomAttribute(out attribute) || !AtataContext.Current.IsNavigated)
            {
                Go.ToUrl(attribute?.Url);
            }
        }

        protected virtual void OnVerify()
        {
        }

        TOther IPageObject.GoTo<TOther>(TOther pageObject, GoOptions options)
        {
            bool isReturnedFromTemporary = pageObject == null && TryResolvePreviousPageObjectNavigatedTemporarily(out pageObject);

            pageObject = pageObject ?? ActivatorEx.CreateInstance<TOther>();

            if (!isReturnedFromTemporary)
            {
                if (!options.Temporarily)
                {
                    AtataContext.Current.CleanUpTemporarilyPreservedPageObjectList();
                }

                pageObject.NavigateOnInit = options.Navigate;

                if (options.Temporarily)
                {
                    pageObject.IsTemporarilyNavigated = options.Temporarily;
                    AtataContext.Current.TemporarilyPreservedPageObjectList.Add(this);
                }
            }

            ExecuteTriggers(TriggerEvents.DeInit);

            // TODO: Review that condition.
            if (!options.Temporarily)
                UIComponentResolver.CleanUpPageObject(this);

            if (!string.IsNullOrWhiteSpace(options.Url))
                Go.ToUrl(options.Url);

            if (!string.IsNullOrWhiteSpace(options.WindowName))
                SwitchTo(options.WindowName);

            if (isReturnedFromTemporary)
            {
                Log.Info("Go to {0}", pageObject.ComponentFullName);
            }
            else
            {
                pageObject.PreviousPageObject = this;
                pageObject.Init();
            }

            return pageObject;
        }

        private bool TryResolvePreviousPageObjectNavigatedTemporarily<TOther>(out TOther pageObject)
            where TOther : PageObject<TOther>
        {
            UIComponent foundPageObject = AtataContext.Current.TemporarilyPreservedPageObjects.
                AsEnumerable().
                Reverse().
                FirstOrDefault(x => x is TOther);

            pageObject = (TOther)foundPageObject;

            if (foundPageObject == null)
                return false;

            var tempPageObjectsToRemove = AtataContext.Current.TemporarilyPreservedPageObjects.
                SkipWhile(x => x != foundPageObject).
                ToArray();

            UIComponentResolver.CleanUpPageObjects(tempPageObjectsToRemove.Skip(1));
            foreach (var item in tempPageObjectsToRemove)
                AtataContext.Current.TemporarilyPreservedPageObjectList.Remove(item);

            return true;
        }

        protected virtual void SwitchTo(string windowName)
        {
            Driver.SwitchTo().Window(windowName);
        }

        /// <summary>
        /// Refreshes the current page.
        /// </summary>
        /// <returns>The instance of this page object.</returns>
        public virtual TOwner RefreshPage()
        {
            Log.Info("Refresh page");
            Driver.Navigate().Refresh();
            return Go.To<TOwner>(navigate: false);
        }

        /// <summary>
        /// Navigates back to the previous page.
        /// </summary>
        /// <typeparam name="TOther">The type of the page object that represents the previous page.</typeparam>
        /// <param name="previousPageObject">The instance of the previous page object. If equals null, creates an instance of <typeparamref name="TOther"/> using the default constructor.</param>
        /// <returns>The instance of the previous page object.</returns>
        public virtual TOther GoBack<TOther>(TOther previousPageObject = null)
            where TOther : PageObject<TOther>
        {
            Log.Info("Go back");
            Driver.Navigate().Back();
            return Go.To(previousPageObject, navigate: false);
        }

        /// <summary>
        /// Navigates forward to the next page.
        /// </summary>
        /// <typeparam name="TOther">The type of the page object that represents the next page.</typeparam>
        /// <param name="nextPageObject">The instance of the next page object. If equals null, creates an instance of <typeparamref name="TOther"/> using the default constructor.</param>
        /// <returns>The instance of the next page object.</returns>
        public virtual TOther GoForward<TOther>(TOther nextPageObject = null)
            where TOther : PageObject<TOther>
        {
            Log.Info("Go forward");
            Driver.Navigate().Forward();
            return Go.To(nextPageObject, navigate: false);
        }

        /// <summary>
        /// Closes the current window.
        /// </summary>
        public virtual void CloseWindow()
        {
            string nextWindowHandle = ResolveWindowHandleToSwitchAfterClose();

            Log.Info("Close window");
            Driver.Close();

            if (nextWindowHandle != null)
                SwitchTo(nextWindowHandle);
        }

        private string ResolveWindowHandleToSwitchAfterClose()
        {
            string currentWindowHandle = Driver.CurrentWindowHandle;
            int indexOfCurrent = Driver.WindowHandles.IndexOf(currentWindowHandle);
            if (indexOfCurrent > 0)
                return Driver.WindowHandles[indexOfCurrent - 1];
            else if (Driver.WindowHandles.Count > 1)
                return Driver.WindowHandles[1];
            else
                return null;
        }

        public TOwner Do<TComponent>(Func<TOwner, TComponent> childControlGetter, Action<TComponent> action)
        {
            action.CheckNotNull(nameof(action));

            TComponent component = childControlGetter((TOwner)this);

            action(component);

            return (TOwner)this;
        }

        public TOwner Do(Action<TOwner> action)
        {
            action.CheckNotNull(nameof(action));

            action((TOwner)this);

            return (TOwner)this;
        }

        public TNavigateTo Do<TNavigateTo>(Func<TOwner, TNavigateTo> navigationAction)
            where TNavigateTo : PageObject<TNavigateTo>
        {
            navigationAction.CheckNotNull(nameof(navigationAction));

            return navigationAction((TOwner)this);
        }

        /// <summary>
        /// Waits the specified time.
        /// </summary>
        /// <param name="time">The time to wait.</param>
        /// <returns>The instance of this page object.</returns>
        public TOwner Wait(TimeSpan time)
        {
            Thread.Sleep(time);

            return (TOwner)this;
        }

        /// <summary>
        /// Waits the specified time in seconds.
        /// </summary>
        /// <param name="seconds">The time to wait in seconds.</param>
        /// <returns>The instance of this page object.</returns>
        public TOwner Wait(double seconds)
        {
            Thread.Sleep((int)(seconds * 1000));

            return (TOwner)this;
        }
    }
}
