Imports Microsoft.Data.Sqlite

Namespace Data

    ' Opens connections to the SQLite database and creates the schema on first use.
    Public Class Database

        Private ReadOnly _connectionString As String

        Public Sub New(connectionString As String)
            _connectionString = connectionString
        End Sub

        Public Function Open() As SqliteConnection
            Dim connection As New SqliteConnection(_connectionString)
            connection.Open()
            Using pragma = connection.CreateCommand()
                ' WAL lets readers continue while a transaction writes; busy_timeout
                ' makes concurrent writers wait instead of failing immediately.
                pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;"
                pragma.ExecuteNonQuery()
            End Using
            Return connection
        End Function

        Public Sub EnsureCreated()
            Using connection = Open()
                Using command = connection.CreateCommand()
                    command.CommandText = Schema
                    command.ExecuteNonQuery()
                End Using
            End Using
        End Sub

        Private Const Schema As String = "
CREATE TABLE IF NOT EXISTS tellers (
    id              INTEGER PRIMARY KEY,
    username        TEXT NOT NULL UNIQUE COLLATE NOCASE,
    display_name    TEXT NOT NULL,
    role            TEXT NOT NULL CHECK (role IN ('Teller', 'Supervisor')),
    password_hash   TEXT NOT NULL,
    failed_attempts INTEGER NOT NULL DEFAULT 0,
    locked_until    TEXT,
    created_at      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS accounts (
    id             INTEGER PRIMARY KEY,
    account_number TEXT NOT NULL UNIQUE,
    holder_name    TEXT NOT NULL,
    -- Whole cents; the CHECK is a last line of defence against overdrawing.
    balance_cents  INTEGER NOT NULL CHECK (balance_cents >= 0),
    status         TEXT NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Frozen')),
    opened_at      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS transactions (
    id                  INTEGER PRIMARY KEY,
    account_id          INTEGER NOT NULL REFERENCES accounts(id),
    type                TEXT NOT NULL CHECK (type IN ('Opening', 'Deposit', 'Withdrawal', 'TransferIn', 'TransferOut')),
    amount_cents        INTEGER NOT NULL CHECK (amount_cents > 0),
    balance_after_cents INTEGER NOT NULL,
    reference           TEXT,
    teller_id           INTEGER NOT NULL REFERENCES tellers(id),
    approved_by_id      INTEGER REFERENCES tellers(id),
    receipt_number      TEXT NOT NULL,
    counterparty_id     INTEGER REFERENCES accounts(id),
    created_at          TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_transactions_account ON transactions(account_id, id DESC);
CREATE INDEX IF NOT EXISTS ix_transactions_receipt ON transactions(receipt_number);
CREATE INDEX IF NOT EXISTS ix_transactions_teller ON transactions(teller_id, created_at);

-- A retried request with the same key returns the original receipt instead of posting twice.
CREATE TABLE IF NOT EXISTS idempotency_keys (
    teller_id      INTEGER NOT NULL REFERENCES tellers(id),
    key            TEXT NOT NULL,
    receipt_number TEXT NOT NULL,
    created_at     TEXT NOT NULL,
    PRIMARY KEY (teller_id, key)
);

CREATE TABLE IF NOT EXISTS audit_log (
    id        INTEGER PRIMARY KEY,
    at        TEXT NOT NULL,
    teller_id INTEGER REFERENCES tellers(id),
    username  TEXT NOT NULL,
    action    TEXT NOT NULL,
    detail    TEXT NOT NULL,
    success   INTEGER NOT NULL,
    remote_ip TEXT
);
CREATE INDEX IF NOT EXISTS ix_audit_at ON audit_log(at DESC);
"

    End Class

End Namespace
