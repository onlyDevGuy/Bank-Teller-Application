# 🏦 Bank Teller Application (VB.NET Windows Forms Project)

## Overview

The Bank Teller Application is a VB.NET Windows Forms desktop application designed to simulate basic banking transactions. The system allows bank tellers to search for customer accounts, perform deposits and withdrawals, update account balances, and track transaction totals during a session.

Account information is stored in a data file, enabling persistent storage between application sessions.

---

## Features

### Account Management
- Search for accounts using account numbers
- Display account holder information
- View current account balance
- Load account data from storage

### Banking Transactions
- Deposit funds
- Withdraw funds
- Update balances automatically
- Save balance changes permanently

### Transaction Reporting
- View total deposits performed
- View total withdrawals performed
- Track transaction activity during the application session

### Data Persistence
- Read account records from file
- Save updated account balances
- Maintain account information between sessions

### Validation
- Account number validation
- Positive transaction amount validation
- Insufficient balance protection
- Error handling and user feedback

---

## Technologies Used

- VB.NET
- Windows Forms
- Object-Oriented Programming (OOP)
- File Handling
- Exception Handling
- Data Validation

---

## Project Structure

```text
Bank Teller Application
│
├── Form1.vb
│   ├── User Interface Logic
│   ├── Account Search
│   ├── Deposits
│   ├── Withdrawals
│   └── Transaction Totals
│
├── Form1.Designer.vb
│   └── Windows Forms Layout
│
├── Account.vb
│   ├── Account Business Logic
│   ├── Data Processing
│   ├── Deposits
│   ├── Withdrawals
│   └── File Operations
│
├── accounts.dat
│   └── Account Data Storage
│
└── App.config
    └── Application Configuration
```

---

## User Interface Features

### Account Information

Users can:

- Enter an account number
- Search for account details
- View account holder name
- View current balance

### Transaction Panel

Users can select:

- Deposit
- Withdraw

and enter the transaction amount.

### Action Buttons

| Button | Function |
|----------|----------|
| Find | Search for account |
| Save Balance | Save transaction |
| Totals | Display transaction totals |
| Close | Exit application |

---

## Application Workflow

### Step 1: Search Account

```text
Enter Account Number
        ↓
Click Find
        ↓
Display Account Details
```

### Step 2: Select Transaction

```text
Deposit
OR
Withdraw
```

### Step 3: Enter Amount

```text
Enter Transaction Amount
```

### Step 4: Save Transaction

```text
Update Balance
        ↓
Save To File
        ↓
Display Confirmation
```

---

## Object-Oriented Concepts Demonstrated

### Encapsulation

Account information is stored within the Account class, including:

- Account Number
- Account Holder Name
- Balance

### Business Logic Separation

The project separates:

- User Interface Logic
- Account Processing Logic
- File Storage Logic

### Data Validation

Validation ensures:

- Positive transaction values
- Valid account numbers
- Safe account updates

---

## Skills Demonstrated

- VB.NET Development
- Windows Forms Development
- Object-Oriented Programming
- File Handling
- Financial Transaction Processing
- Data Validation
- Exception Handling
- Desktop Application Development

---

## Key Learning Outcomes

This project demonstrates:

- Creating Windows Desktop Applications
- Working with Forms and Controls
- Managing Business Logic
- Persistent File Storage
- Banking Transaction Processing
- User Input Validation
- Software Design Principles

---

## Real-World Applications

The concepts used in this project can be applied to:

- Banking Systems
- ATM Software
- Financial Management Applications
- Point-of-Sale Systems
- Customer Account Management Systems

---

## Example Use Cases

### Deposit Transaction

```text
Account Balance: R5,000.00

Deposit: R1,000.00

New Balance:
R6,000.00
```

### Withdrawal Transaction

```text
Account Balance: R5,000.00

Withdraw: R750.00

New Balance:
R4,250.00
```

---

## Future Improvements

- SQL Database Integration
- Customer Registration Module
- Transaction History
- Interest Calculations
- User Authentication
- Account Creation and Closure
- Transfer Between Accounts
- Reporting Dashboard

---

## Author

**Sizwe Ramokhali**

Software Developer | Software Developer

### Skills Demonstrated
- VB.NET Development
- Windows Forms
- Object-Oriented Design
- Financial Application Development
- File Processing
- Data Validation
- Desktop Application Development
