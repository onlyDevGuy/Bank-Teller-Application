Imports System.Globalization
Imports System.Runtime.CompilerServices
Imports Microsoft.Data.Sqlite

Namespace Data

    Public Module SqlExtensions

        ' Timestamps are stored as round-trip ISO 8601 UTC text ("o" format).
        <Extension>
        Public Function GetTimestamp(reader As SqliteDataReader, ordinal As Integer) As DateTimeOffset
            Return DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        End Function

        <Extension>
        Public Function GetStringOrNothing(reader As SqliteDataReader, ordinal As Integer) As String
            Return If(reader.IsDBNull(ordinal), Nothing, reader.GetString(ordinal))
        End Function

        <Extension>
        Public Function GetInt64OrNothing(reader As SqliteDataReader, ordinal As Integer) As Long?
            Return If(reader.IsDBNull(ordinal), CType(Nothing, Long?), reader.GetInt64(ordinal))
        End Function

        ' Adds a parameter, mapping Nothing to SQL NULL.
        <Extension>
        Public Sub Bind(command As SqliteCommand, name As String, value As Object)
            command.Parameters.AddWithValue(name, If(value, DBNull.Value))
        End Sub

        Public Function ToTimestamp(value As DateTimeOffset) As String
            Return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
        End Function

    End Module

End Namespace
