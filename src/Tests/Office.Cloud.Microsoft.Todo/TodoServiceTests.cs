﻿using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using akarnokd.reactive_extensions;
using DevExpress.ExpressApp;
using Microsoft.Graph;
using NUnit.Framework;
using Shouldly;
using Xpand.Extensions.Office.Cloud;
using Xpand.Extensions.Reactive.Conditional;
using Xpand.Extensions.Reactive.Filter;
using Xpand.Extensions.Reactive.Transform;
using Xpand.TestsLib.Attributes;
using Xpand.XAF.Modules.Office.Cloud.Tests;
using Xpand.XAF.Modules.Reactive;
using Platform = Xpand.Extensions.XAF.XafApplicationExtensions.Platform;
using TaskStatus = DevExpress.Persistent.Base.General.TaskStatus;


namespace Xpand.XAF.Modules.Office.Cloud.Microsoft.Todo.Tests{
    [NonParallelizable]
    public class TodoServiceTests : BaseTaskTests{
        [TestCase(TaskStatus.NotStarted,nameof(TaskStatus.NotStarted))]
        [TestCase(TaskStatus.Completed,nameof(TaskStatus.Completed))]
        [XpandTest()]
        public override async Task Map_Two_New_Tasks(TaskStatus projectTaskStatus, string taskStatus){
            await MapTwoNewTasks(projectTaskStatus, taskStatus,TodoTestExtensions.TasksFolderName);
        }

        private async Task MapTwoNewTasks(TaskStatus projectTaskStatus, string taskStatus, string tasksFolderName,Func<IOutlookTaskFolderRequestBuilder,IObservable<Unit>> afterAssert=null,bool keepTaskFolder=false){
        
            using (var application = Platform.Win.TodoModule().Application){
                (IOutlookTaskFolderRequestBuilder requestBuilder, Frame frame) builder = await application.InitializeService(tasksFolderName,keepTaskFolder:keepTaskFolder);
                await builder.frame.View.ObjectSpace.Map_Two_New_Entity(
                    (space, i) => space.NewTask(projectTaskStatus, i), Timeout,
                    space => TodoService.Updated.TakeUntilDisposed(application), async (task, _, i) => {
                        if (afterAssert != null){
                            await afterAssert(builder.requestBuilder);
                        }
                        else{
                            application.ObjectSpaceProvider.AssertTask(typeof(OutlookTask), task, _.serviceObject.Subject,
                                _.serviceObject.Body.Content,
                                DateTime.Parse(_.serviceObject.DueDateTime.DateTime, CultureInfo.InvariantCulture),
                                taskStatus,
                                $"{_.serviceObject.Status}", _.serviceObject.Id, task.Subject);    
                        }
                            
                    });
            }
        }

        [Test]
        [XpandTest()]
        public override async Task Customize_Two_New_Tasks(){
            using (var application = Platform.Win.TodoModule().Application){
                var builder = await application.InitializeService();
                await builder.frame.View.ObjectSpace.Map_Two_New_Entity((space,i)=>space.NewTask(TaskStatus.NotStarted,i), Timeout,
                    space => TodoService.Updated.Select(_ => _.serviceObject).Merge(TodoService.CustomizeInsert
                        .Select((tuple, i) => {
                            tuple.outlookTask.Subject = $"{nameof(Customize_Two_New_Tasks)}{i}";
                            return default(OutlookTask);
                        }).IgnoreElements()).TakeUntilDisposed(application).Select(task => task), 
                    (task, taskListEntry,i) => {
                        application.ObjectSpaceProvider.AssertTask(typeof(OutlookTask),task, taskListEntry.Subject, taskListEntry.Body.Content, DateTime.Parse(taskListEntry.DueDateTime.DateTime),
                            TaskStatus.NotStarted.ToString(), taskListEntry.Status.ToString(), taskListEntry.Id,$"{nameof(Customize_Two_New_Tasks)}{i}");
                        
                    });
            
            }
        }

        [TestCase(TaskStatus.NotStarted,nameof(TaskStatus.NotStarted))]
        [TestCase(TaskStatus.Completed,nameof(TaskStatus.Completed))]
        [XpandTest()]
        public override async Task Map_Existing_Two_Times(TaskStatus projectTaskStatus, string taskStatus){
            using (var application = Platform.Win.TodoModule().Application){
                var builderData = await application.InitializeService();
                var existingObjects = (await application.CreateExistingObjects(nameof(Map_Existing_Two_Times), projectTaskStatus)).First();

                await builderData.frame.View.ObjectSpace.Map_Existing_Entity_Two_Times(existingObjects.task,
                    (pmeTask,i) => pmeTask.Modify_Task( projectTaskStatus,i), existingObjects.outlookTask
                    , space => TodoService.Updated.Select(_ => _.serviceObject).TakeUntilDisposed(application),
                    (task, outlookTask) => {
                        outlookTask.Subject.ShouldBe(task.Subject);
                        outlookTask.Status.ToString().ShouldBe(taskStatus);
                        return Task.CompletedTask;
                    },Timeout);
            }
        }
        
        [Test][XpandTest()]
        public override async Task Customize_Existing_Two_Times(){
            using (var application = Platform.Win.TodoModule().Application){
                var builder = await application.InitializeService();
                var existingObjects = (await application.CreateExistingObjects(nameof(Customize_Existing_Two_Times))).First();
                await builder.frame.View.ObjectSpace.Map_Existing_Entity_Two_Times(existingObjects.task,
                    (pmeTask,i) => pmeTask.Modify_Task( TaskStatus.Completed, i),existingObjects.outlookTask, space
                        => TodoService.Updated.Select(_ => _.serviceObject).Merge(TodoService.CustomizeUpdate.Take(2)
                            .Do(_ => _.outlookTask.Subject = nameof(Customize_Existing_Two_Times)).To(default(OutlookTask)).IgnoreElements())
                            .TakeUntilDisposed(application),
                    (pmeTask, cloudTask) => {
                        cloudTask.Subject.ShouldBe(nameof(Customize_Existing_Two_Times));
                        cloudTask.Status.ShouldBe(global::Microsoft.Graph.TaskStatus.Completed);
                        return Task.CompletedTask;
                    },Timeout);
            }
        }

        [Test]
        [XpandTest()]
        public override async Task Delete_Two_Tasks(){
            using (var application = Platform.Win.TodoModule().Application){
                var builder = await application.InitializeService();
                var existingObjects = await application.CreateExistingObjects(nameof(Delete_Two_Tasks),count:2);

                await builder.frame.View.ObjectSpace.Delete_Two_Entities(existingObjects.Select(tuple => tuple.task).ToArray(),
                    space => TodoService.Updated.Select(_ => _.serviceObject).TakeUntilDisposed(application), async () => {
                        var allTasks = await builder.requestBuilder.Tasks.ListAllItems();
                        allTasks.Length.ShouldBe(0);
                    }, Timeout,existingObjects.Select(_ => _.outlookTask).ToArray());
            }
        }
        
        [TestCase(true)]
        [TestCase(false)]
        [XpandTest()]
        public override async Task Customize_Delete_Two_Tasks(bool handleDeletion){
            using (var application = Platform.Win.TodoModule().Application){
                var builder = await application.InitializeService();
                var existingObjects = await application.CreateExistingObjects(nameof(Customize_Delete_Two_Tasks),count:2);
                var deleteTwoEntities = builder.frame.View.ObjectSpace.Delete_Two_Entities(existingObjects.Select(_ => _.task).ToArray(),
                    objectSpace => TodoService.CustomizeDelete.Take(2)
                        .Do(_ => _.Handled=handleDeletion).To(default(OutlookTask))
                        .TakeUntilDisposed(application),
                    async () => {
                        var allTasks = await builder.requestBuilder.Tasks.ListAllItems();
                        allTasks.Length.ShouldBe(handleDeletion?2:0);
                    }, Timeout,existingObjects.Select(_ => _.outlookTask).ToArray());
                await deleteTwoEntities;
            }
        }


        [TestCase(null)][XpandTest()]
        public override async Task Populate_All(string syncToken){
            using (var application = Platform.Win.TodoModule().Application){
                var builder = await application.InitializeService(TodoTestExtensions.TasksPagingFolderName);
                await builder.frame.View.ObjectSpace.Populate_All(syncToken,
                    storage => builder.requestBuilder.Tasks.ListAllItems(
                        store => store.SaveToken(application.ObjectSpaceProvider.CreateObjectSpace), storage), Timeout,
                    tasks => {
                        tasks.Test().ItemCount.ShouldBe(2);
                        tasks.SelectMany(events1 => events1).Test().ItemCount.ShouldBe(TodoTestExtensions.TasksFolderPagingItemsCount);
                    });
            }
        }

        public override Task Populate_Modified(){
            throw new NotImplementedException();
        }

        [XpandTest()]
        [Test]
        public override async Task Create_Entity_Container_When_Not_Exist(){
            using (var application = Platform.Win.TodoModule().Application){
                var modelTodo = application.Model.ToReactiveModule<IModelReactiveModuleOffice>().Office.Microsoft().Todo();
                modelTodo.DefaultTodoListName = $"{nameof(Create_Entity_Container_When_Not_Exist)}{Guid.NewGuid()}";

                var serviceClient = await application.InitGraphServiceClient();

                var outlookTaskFolders = serviceClient.client.Me().Outlook.TaskFolders;
                var folder = await outlookTaskFolders.GetFolder(modelTodo.DefaultTodoListName).WhenNotDefault().FirstAsync();
                await outlookTaskFolders[folder.Id].Request().DeleteAsync();
            }

            
            
        }

        // [XpandTest()]
        public override Task User_Container(){
            throw new NotImplementedException();
        }
    }
}