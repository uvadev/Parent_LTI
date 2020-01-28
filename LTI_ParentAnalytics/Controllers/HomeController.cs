﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using UVACanvasAccess.Util;
using UVACanvasAccess.ApiParts;
using UVACanvasAccess.Structures.Users;
using UVACanvasAccess.Structures.Courses;
using static UVACanvasAccess.ApiParts.Api;
using UVACanvasAccess.Structures.Analytics;
using UVACanvasAccess.Structures.Assignments;

namespace LTI_ParentAnalytics.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult ObserveeInfo()
        {
            //string GetEnvironmentVariable(string token, System.EnvironmentVariableTarget target);
            string userIdSt = Request.Form.Get("custom_canvas_user_id");

            ulong userId = Convert.ToUInt64(userIdSt);

            string token = Environment.GetEnvironmentVariable("API_KEY");

            var api = new Api(token, "https://uview.instructure.com/api/v1/");

            var uId = api.StreamObservees(userId);

            var kidList = uId.CollectAsync().Result;
            
            ViewBag.Entries = kidList;

            var coursesByStudent = new Dictionary<ulong, IEnumerable<Course>>(); // userId -> course[]
            var courseDataByStudent = new Dictionary<ulong, Dictionary<ulong, UserParticipation>>(); // userId -> { courseId -> data }
            var participationsByStudent = new Dictionary<ulong, Dictionary<ulong, IEnumerable<UserParticipationEvent>>>();
            //var assDataByStudent = new Dictionary<ulong, Dictionary<ulong, IEnumerable<UserAssignmentData>>>();

            foreach (var kid in kidList)
            {
                coursesByStudent.Add(kid.Id, api.StreamUserEnrollments(kid.Id,
                                                                       states: new[] { CourseEnrollmentState.Active })
                                                .CollectAsync()
                                                .Result
                                                .GroupBy(e => e.CourseId)
                                                .Select(e => e.First())
                                                .ToList()
                                                .Select(e => api.GetCourse(e.CourseId,
                                                                           includes: IndividualLevelCourseIncludes.Term)
                                                                .Result)
                                    );
                var streamMissingAssignments = api.StreamMissingAssignments(kid.Id);
                
                ViewBag.StreamMissingAssignments = streamMissingAssignments.CollectAsync().Result;
                
                ViewBag.Courses = coursesByStudent;
                courseDataByStudent.Add(kid.Id, new Dictionary<ulong, UserParticipation>());
                participationsByStudent.Add(kid.Id, new Dictionary<ulong, IEnumerable<UserParticipationEvent>>());
                //assDataByStudent.Add(kid.Id, new Dictionary<ulong, IEnumerable<UserAssignmentData>>()); 

                foreach (var course in coursesByStudent[kid.Id])
                {
                    var data = api.GetUserCourseParticipationData(kid.Id, course.Id).Result;
                    courseDataByStudent[kid.Id].Add(course.Id, data);
                    participationsByStudent[kid.Id].Add(course.Id, data.Participations.Reverse()
                                                                                      .Take(10));

                    //var assData = api.GetUserCourseAssignmentData(kid.Id, course.Id).CollectAsync()
                    //                                                                .Result;
                    //assDataByStudent[kid.Id].Add(course.Id, assData);
                }

                //ViewBag.AssDataByStudent = assDataByStudent;
            }

            ViewBag.CourseDataByStudent = courseDataByStudent;
            ViewBag.ParticipationsByStudent = participationsByStudent;

            return View();

            //collect async => stream.CollectAsync().Result
            //collect async => stream.ToPrettyStringAsync().Result
            //print entire string
            //replicate for summary
        }
    }
}