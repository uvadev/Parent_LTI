using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using UVACanvasAccess.ApiParts;
using UVACanvasAccess.Structures.Analytics;
using UVACanvasAccess.Structures.Courses;
using UVACanvasAccess.Util;
using static UVACanvasAccess.ApiParts.Api;

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
            var userIdSt = Request.Form.Get("custom_canvas_user_id");

            var userId = Convert.ToUInt64(userIdSt);

            var token = Environment.GetEnvironmentVariable("API_KEY");

            var api = new Api(token, "https://uview.instructure.com/api/v1/");

            var uId = api.StreamObservees(userId);

            var kidList = uId.CollectAsync().Result;

            ViewBag.Entries = kidList;

            var coursesByStudent = new Dictionary<ulong, IEnumerable<Course>>(); // userId -> course[]
            var courseDataByStudent =
                new Dictionary<ulong, Dictionary<ulong, UserParticipation>>(); // userId -> { courseId -> data }
            var participationsByStudent =
                new Dictionary<ulong, Dictionary<ulong, IEnumerable<UserParticipationEvent>>>();

            foreach (var kid in kidList)
            {
                coursesByStudent.Add(kid.Id, api.StreamUserEnrollments(kid.Id,
                        states: new[] {CourseEnrollmentState.Active})
                    .CollectAsync()
                    .Result
                    .GroupBy(e => e.CourseId)
                    .Select(e => e.First())
                    .ToList()
                    .Select(e => api.GetCourse(e.CourseId,
                            includes: IndividualLevelCourseIncludes.Term)
                        .Result)
                );

                ViewBag.Courses = coursesByStudent;
                courseDataByStudent.Add(kid.Id, new Dictionary<ulong, UserParticipation>());
                participationsByStudent.Add(kid.Id, new Dictionary<ulong, IEnumerable<UserParticipationEvent>>());

                foreach (var course in coursesByStudent[kid.Id])
                {
                    var data = api.GetUserCourseParticipationData(kid.Id, course.Id).Result;
                    courseDataByStudent[kid.Id].Add(course.Id, data);
                    participationsByStudent[kid.Id].Add(course.Id, data.Participations.Reverse()
                        .Take(10));
                }

                //ViewBag.AssDataByStudent = assDataByStudent;
            }

            ViewBag.CourseDataByStudent = courseDataByStudent;
            ViewBag.ParticipationsByStudent = participationsByStudent;

            return View();
        }
    }
}