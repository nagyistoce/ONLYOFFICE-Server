/*
 * 
 * (c) Copyright Ascensio System SIA 2010-2014
 * 
 * This program is a free software product.
 * You can redistribute it and/or modify it under the terms of the GNU Affero General Public License
 * (AGPL) version 3 as published by the Free Software Foundation. 
 * In accordance with Section 7(a) of the GNU AGPL its Section 15 shall be amended to the effect 
 * that Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 * 
 * This program is distributed WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * For details, see the GNU AGPL at: http://www.gnu.org/licenses/agpl-3.0.html
 * 
 * You can contact Ascensio System SIA at Lubanas st. 125a-25, Riga, Latvia, EU, LV-1021.
 * 
 * The interactive user interfaces in modified source and object code versions of the Program 
 * must display Appropriate Legal Notices, as required under Section 5 of the GNU AGPL version 3.
 * 
 * Pursuant to Section 7(b) of the License you must retain the original Product logo when distributing the program. 
 * Pursuant to Section 7(e) we decline to grant you any rights under trademark law for use of our trademarks.
 * 
 * All the Product's GUI elements, including illustrations and icon sets, as well as technical 
 * writing content are licensed under the terms of the Creative Commons Attribution-ShareAlike 4.0 International. 
 * See the License terms at http://creativecommons.org/licenses/by-sa/4.0/legalcode
 * 
*/

using System;
using System.Web;

namespace ASC.Web.Studio.Controls.FileUploader.HttpModule
{
    internal class EntityBodyInspector
    {
        private EntityBodyChunkStateWaiter _current;
        private string _lastCdName;
        private readonly EntityBodyChunkStateWaiter _boundaryWaiter;
        private readonly EntityBodyChunkStateWaiter _boundaryInfoWaiter;
        private readonly EntityBodyChunkStateWaiter _formValueWaiter;
        private readonly UploadProgressStatistic _statistic;

        internal EntityBodyInspector(HttpUploadWorkerRequest request)
        {
            _statistic = new UploadProgressStatistic
                {
                    TotalBytes = request.GetTotalEntityBodyLength()
                };

            var contentType = request.GetKnownRequestHeader(HttpWorkerRequest.HeaderContentType);

            var boundary = string.Format("--{0}\r\n", UploadProgressUtils.GetBoundary(contentType));

            _boundaryWaiter = new EntityBodyChunkStateWaiter(boundary, false);
            _boundaryWaiter.MeetGuard += BoundaryWaiterMeetGuard;
            _current = _boundaryWaiter;

            _boundaryInfoWaiter = new EntityBodyChunkStateWaiter("\r\n\r\n", true);
            _boundaryInfoWaiter.MeetGuard += BoundaryInfoWaiterMeetGuard;

            _formValueWaiter = new EntityBodyChunkStateWaiter("\r\n", true);
            _formValueWaiter.MeetGuard += FormValueWaiterMeetGuard;

            _lastCdName = string.Empty;
        }

        internal void EndRequest()
        {
            _statistic.EndUpload();
        }

        internal void Inspect(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
                return;

            _statistic.AddUploadedBytes(buffer.Length);
            Inspect(buffer, offset);
        }

        private void Inspect(byte[] buffer, int offset)
        {
            if (buffer == null)
                return;

            _current.Wait(buffer, offset);
        }

        private void BoundaryWaiterMeetGuard(object sender, EventArgs e)
        {
            var sw = sender as EntityBodyChunkStateWaiter;
            sw.Reset();
            _current = _boundaryInfoWaiter;
            _current.Wait(sw);
        }

        private void BoundaryInfoWaiterMeetGuard(object sender, EventArgs e)
        {
            var sw = sender as EntityBodyChunkStateWaiter;
            var cdi = UploadProgressUtils.GetContentDisposition(sw.Value);
            sw.Reset();
            if (!cdi.IsFile)
            {
                _lastCdName = cdi.name;
                _current = _formValueWaiter;
                _current.Wait(sw);
            }
            else
            {
                _statistic.BeginFileUpload(cdi.filename);
                _current = _boundaryWaiter;
                _current.Wait(sw);
            }
        }

        private void FormValueWaiterMeetGuard(object sender, EventArgs e)
        {
            var sw = sender as EntityBodyChunkStateWaiter;

            var fieldValue = sw.Value;
            _statistic.AddFormField(_lastCdName, fieldValue);

            sw.Reset();

            _current = _boundaryWaiter;
            _current.Wait();
        }
    }
}