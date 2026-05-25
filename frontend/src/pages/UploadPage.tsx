import { useState } from 'react'
import { Link } from 'react-router-dom'
import api from '../api/client'

interface DuplicateRecord {
  workerId: string
  siteName: string
  period: string
  existingDaysPresent: number
  existingDayRate: number
  newDaysPresent: number
  newDayRate: number
}

interface UploadResult {
  uploadId: number
  totalRows: number
  validRows: number
  errorCount: number
  errors: string[]
  duplicates: DuplicateRecord[]
  hasDuplicates: boolean
}

export default function UploadPage() {
  const [file, setFile] = useState<File | null>(null)
  const [uploading, setUploading] = useState(false)
  const [result, setResult] = useState<UploadResult | null>(null)
  const [calculating, setCalculating] = useState(false)
  const [calculated, setCalculated] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [duplicatesResolved, setDuplicatesResolved] = useState(false)

  const handleUpload = async () => {
    if (!file) return
    setUploading(true)
    setError(null)
    setResult(null)
    setDuplicatesResolved(false)

    try {
      const formData = new FormData()
      formData.append('file', file)
      const res = await api.post<UploadResult>('/upload', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      setResult(res.data)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Upload failed'
      setError(msg)
    } finally {
      setUploading(false)
    }
  }

  const handleConfirmOverwrite = async () => {
    if (!result) return
    setConfirming(true)
    setError(null)

    try {
      await api.post('/upload/confirm-overwrite', { uploadId: result.uploadId })
      setDuplicatesResolved(true)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Overwrite failed'
      setError(msg)
    } finally {
      setConfirming(false)
    }
  }

  const handleCancelUpload = async () => {
    if (!result) return
    setConfirming(true)
    setError(null)

    try {
      await api.post('/upload/cancel', { uploadId: result.uploadId })
      setResult(null)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Cancel failed'
      setError(msg)
    } finally {
      setConfirming(false)
    }
  }

  const handleCalculate = async () => {
    if (!result) return
    setCalculating(true)
    setError(null)

    try {
      await api.post('/payments/calculate', { uploadId: result.uploadId })
      setCalculated(true)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Calculation failed'
      setError(msg)
    } finally {
      setCalculating(false)
    }
  }

  return (
    <div className="max-w-3xl">
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-slate-100">Upload Attendance</h2>
        <p className="text-sm text-slate-500 mt-1">Import worker attendance data for payment calculation</p>
      </div>

      {/* File input */}
      <div className="border-2 border-dashed border-slate-700 rounded-xl p-10 text-center mb-5 bg-slate-900 hover:border-indigo-500/50 hover:bg-slate-900/80 transition-all duration-200">
        <div className="text-4xl mb-3">{'\u{1F4C4}'}</div>
        <input
          type="file"
          accept=".xlsx"
          onChange={(e) => {
            setFile(e.target.files?.[0] || null)
            setResult(null)
            setCalculated(false)
            setDuplicatesResolved(false)
          }}
          className="block w-full text-sm text-slate-400 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:text-sm file:font-semibold file:bg-indigo-600 file:text-white hover:file:bg-indigo-500 file:cursor-pointer file:transition-colors"
        />
        <p className="mt-3 text-xs text-slate-500">
          Expected columns: WorkerId, Site, DaysPresent, DayRate (.xlsx only)
        </p>
      </div>

      <button
        onClick={handleUpload}
        disabled={!file || uploading}
        className="px-5 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98] transition-all"
      >
        {uploading ? 'Uploading...' : 'Upload File'}
      </button>

      {/* Error */}
      {error && (
        <div className="mt-4 p-3 bg-red-900/30 border border-red-800 rounded-lg text-red-400 text-sm">
          {error}
        </div>
      )}

      {/* Results */}
      {result && (
        <div className="mt-6 p-5 bg-slate-900 border border-slate-800 rounded-xl">
          <h3 className="font-semibold text-slate-200 mb-3">Upload Result</h3>
          <div className="grid grid-cols-3 gap-4 text-center">
            <div className="bg-slate-800 rounded-lg p-3">
              <p className="text-2xl font-bold text-indigo-400">{result.totalRows}</p>
              <p className="text-[10px] text-slate-500 uppercase">Total Rows</p>
            </div>
            <div className="bg-slate-800 rounded-lg p-3">
              <p className="text-2xl font-bold text-emerald-400">{result.validRows}</p>
              <p className="text-[10px] text-slate-500 uppercase">Valid</p>
            </div>
            <div className="bg-slate-800 rounded-lg p-3">
              <p className="text-2xl font-bold text-red-400">{result.errorCount}</p>
              <p className="text-[10px] text-slate-500 uppercase">Errors</p>
            </div>
          </div>

          {result.errors.length > 0 && (
            <div className="mt-4 p-3 bg-amber-900/20 border border-amber-800/50 rounded-lg text-sm">
              <p className="font-medium text-amber-400 mb-1">Validation Errors:</p>
              <ul className="list-disc list-inside text-amber-300/80 space-y-1">
                {result.errors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Duplicate confirmation */}
          {result.hasDuplicates && !duplicatesResolved && (
            <div className="mt-4 p-4 bg-orange-900/20 border border-orange-800/50 rounded-lg">
              <p className="font-medium text-orange-400 mb-2">
                {'\u26A0\uFE0F'} Duplicate Records Found ({result.duplicates.length})
              </p>
              <p className="text-sm text-orange-300/80 mb-3">
                The following workers already have attendance records for this period and site. Do you want to overwrite them?
              </p>
              <div className="overflow-x-auto mb-3">
                <table className="min-w-full text-xs border border-slate-700 rounded">
                  <thead className="bg-slate-800">
                    <tr>
                      <th className="px-2 py-1.5 text-left text-slate-400">Worker</th>
                      <th className="px-2 py-1.5 text-left text-slate-400">Site</th>
                      <th className="px-2 py-1.5 text-left text-slate-400">Period</th>
                      <th className="px-2 py-1.5 text-right text-slate-400">Old Days</th>
                      <th className="px-2 py-1.5 text-right text-slate-400">New Days</th>
                      <th className="px-2 py-1.5 text-right text-slate-400">Old Rate</th>
                      <th className="px-2 py-1.5 text-right text-slate-400">New Rate</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800">
                    {result.duplicates.map((d, i) => (
                      <tr key={i}>
                        <td className="px-2 py-1.5 font-medium text-slate-300">{d.workerId}</td>
                        <td className="px-2 py-1.5 text-slate-400">{d.siteName}</td>
                        <td className="px-2 py-1.5 text-slate-400">{d.period}</td>
                        <td className="px-2 py-1.5 text-right text-red-400">{d.existingDaysPresent}</td>
                        <td className="px-2 py-1.5 text-right text-emerald-400">{d.newDaysPresent}</td>
                        <td className="px-2 py-1.5 text-right text-red-400">{'\u20B9'}{d.existingDayRate}</td>
                        <td className="px-2 py-1.5 text-right text-emerald-400">{'\u20B9'}{d.newDayRate}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="flex gap-3">
                <button
                  onClick={handleConfirmOverwrite}
                  disabled={confirming}
                  className="px-4 py-2 bg-orange-600 text-white text-sm rounded-lg hover:bg-orange-500 disabled:opacity-50"
                >
                  {confirming ? 'Processing...' : 'Overwrite Existing'}
                </button>
                <button
                  onClick={handleCancelUpload}
                  disabled={confirming}
                  className="px-4 py-2 bg-slate-700 text-slate-300 text-sm rounded-lg hover:bg-slate-600 disabled:opacity-50"
                >
                  Cancel Upload
                </button>
              </div>
            </div>
          )}

          {duplicatesResolved && (
            <div className="mt-4 p-3 bg-emerald-900/20 border border-emerald-800/50 rounded-lg text-emerald-400 text-sm">
              {'\u2713'} Duplicate records have been overwritten successfully.
            </div>
          )}

          {/* Calculate button */}
          {!calculated && (!result.hasDuplicates || duplicatesResolved) && (
            <button
              onClick={handleCalculate}
              disabled={calculating}
              className="mt-4 px-5 py-2.5 bg-emerald-600 text-white text-sm font-medium rounded-lg hover:bg-emerald-500 disabled:opacity-50 active:scale-[0.98] transition-all"
            >
              {calculating ? 'Calculating...' : 'Calculate Payments'}
            </button>
          )}

          {calculated && (
            <div className="mt-4 p-3 bg-emerald-900/20 border border-emerald-800/50 rounded-lg text-emerald-400 text-sm">
              Payments calculated successfully. Go to the <Link to="/dashboard" className="font-semibold underline hover:text-emerald-300">Dashboard</Link> to view results.
            </div>
          )}
        </div>
      )}
    </div>
  )
}
