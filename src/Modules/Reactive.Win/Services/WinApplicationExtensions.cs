﻿using System;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using Xpand.XAF.Modules.Reactive.Services;

namespace Xpand.XAF.Modules.Reactive.Win.Services{
    public static class WinApplicationExtensions{
        public static IObservable<Window> WhenMainFormVisible(this XafApplication xafApplication){
            return xafApplication.WhenWindowCreated().When(TemplateContext.ApplicationWindow)
                .SelectMany(window =>
                    window.Template.ToForm().WhenVisibleChanged().Where(_ => _.Visible).Select(form => window));
        }

        public static IObservable<Window> WhenMainFormShown(this XafApplication xafApplication){
            return xafApplication.WhenWindowCreated().When(TemplateContext.ApplicationWindow)
                .SelectMany(window => window.Template.ToForm().WhenShown().Select(form => window));
        }

    }
}