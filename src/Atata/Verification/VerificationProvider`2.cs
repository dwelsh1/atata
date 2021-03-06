﻿using System;

namespace Atata
{
    public abstract class VerificationProvider<TVerificationProvider, TOwner> : IVerificationProvider<TOwner>
        where TVerificationProvider : VerificationProvider<TVerificationProvider, TOwner>
        where TOwner : PageObject<TOwner>
    {
        private readonly bool isNegation;

        protected VerificationProvider(bool isNegation = false)
        {
            this.isNegation = isNegation;
        }

        bool IVerificationProvider<TOwner>.IsNegation => isNegation;

        TOwner IVerificationProvider<TOwner>.Owner
        {
            get { return Owner; }
        }

        protected abstract TOwner Owner { get; }

        protected internal TimeSpan? Timeout { get; internal set; }

        protected internal TimeSpan? RetryInterval { get; internal set; }

        TimeSpan? IVerificationProvider<TOwner>.Timeout => Timeout;

        TimeSpan? IVerificationProvider<TOwner>.RetryInterval => RetryInterval;

        public TVerificationProvider WithRetry
        {
            get
            {
                Timeout = AtataContext.Current.RetryTimeout;
                RetryInterval = AtataContext.Current.RetryInterval;

                return (TVerificationProvider)this;
            }
        }

        public TVerificationProvider AtOnce
        {
            get
            {
                Timeout = TimeSpan.Zero;

                return (TVerificationProvider)this;
            }
        }

        public TVerificationProvider Within(TimeSpan timeout, TimeSpan? retryInterval = null)
        {
            Timeout = timeout;
            RetryInterval = retryInterval ?? RetryInterval;

            return (TVerificationProvider)this;
        }

        public TVerificationProvider Within(double timeoutSeconds, double? retryIntervalSeconds = null)
        {
            return Within(TimeSpan.FromSeconds(timeoutSeconds), retryIntervalSeconds.HasValue ? (TimeSpan?)TimeSpan.FromSeconds(retryIntervalSeconds.Value) : null);
        }

        string IVerificationProvider<TOwner>.GetShouldText()
        {
            return isNegation ? "should not" : "should";
        }

        internal TVerificationProvider ApplySettings(IVerificationProvider<TOwner> verificationProvider)
        {
            Timeout = verificationProvider.Timeout;
            RetryInterval = verificationProvider.RetryInterval;

            return (TVerificationProvider)this;
        }
    }
}
