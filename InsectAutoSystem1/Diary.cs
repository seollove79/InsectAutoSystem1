using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InsectAutoSystem1
{
    class Diary
    {
        private string requestUrl = "http://59.15.133.179:23500/createDiary/GROWTH/누에계통1사육상자1";

        public Diary()
        {

        }

        public async Task post()
        {
            JObject bodyMessage = new JObject();
            bodyMessage.Add("note", "메모입니다.");

            JArray tags = new JArray();
            JObject tagCO2 = new JObject();
            JObject tagNH3 = new JObject();
            JObject tagHUMID = new JObject();

            tagCO2.Add("name", "CO2");
            tagCO2.Add("value", 1);
            tags.Add(tagCO2);

            tagNH3.Add("name", "NH3");
            tagNH3.Add("value", 2);
            tags.Add(tagNH3);

            tagHUMID.Add("name", "박스습도");
            tagHUMID.Add("value", 25);
            tags.Add(tagHUMID);

            bodyMessage.Add("tags", tags);

            Console.WriteLine(bodyMessage.ToString());

            var client = new HttpClient();
            client.DefaultRequestHeaders.ExpectContinue = false; // <-- Make sure it is false.
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpContent = new StringContent(bodyMessage.ToString(), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(requestUrl, httpContent);

            // 응답 데이터 가져오기
            var responseJson = await response.Content.ReadAsStringAsync();

            // 응답 데이터 역직렬화
            var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);
        }
    }
}
