// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

import "@openzeppelin/contracts/security/ReentrancyGuard.sol";
import "@openzeppelin/contracts/access/AccessControl.sol";
import "@openzeppelin/contracts/security/Pausable.sol";
import "@openzeppelin/contracts/utils/Counters.sol";

/**
 * @title LKS IP Patent Smart Contract
 * @dev Blockchain-based intellectual property patent registration and management
 * @notice This contract handles patent submissions, reviews, and blockchain storage
 * 
 * Security Features:
 * - Immutable patent records once approved
 * - Multi-signature approval process
 * - Comprehensive audit trails
 * - Access control for different roles
 * - Emergency pause functionality
 * 
 * Audit Compliance:
 * - Follows Certik security standards
 * - Complete event logging for transparency
 * - Input validation and overflow protection
 * - Role-based access control implementation
 */
contract LKSIPPatent is ReentrancyGuard, AccessControl, Pausable {
    using Counters for Counters.Counter;

    // Role definitions
    bytes32 public constant EXAMINER_ROLE = keccak256("EXAMINER_ROLE");
    bytes32 public constant REVIEWER_ROLE = keccak256("REVIEWER_ROLE");
    bytes32 public constant PUBLISHER_ROLE = keccak256("PUBLISHER_ROLE");
    bytes32 public constant AUDITOR_ROLE = keccak256("AUDITOR_ROLE");

    // Patent status enumeration
    enum PatentStatus {
        Submitted,      // 0 - Initial submission
        UnderReview,    // 1 - Being reviewed by examiners
        Approved,       // 2 - Approved for blockchain storage
        Rejected,       // 3 - Rejected with reasons
        Published,      // 4 - Published on blockchain
        Revoked         // 5 - Revoked due to issues
    }

    // Patent structure for comprehensive IP management
    struct Patent {
        uint256 id;
        address applicant;
        string title;
        string description;
        string[] claims;
        string[] inventors;
        string assignee;
        PatentStatus status;
        uint256 submissionDate;
        uint256 reviewDate;
        uint256 approvalDate;
        uint256 publicationDate;
        bytes32 documentHash;
        bytes32 priorArtHash;
        string rejectionReason;
        address examiner;
        address reviewer;
        bool isActive;
    }

    // State variables
    Counters.Counter private _patentIdCounter;
    mapping(uint256 => Patent) private _patents;
    mapping(address => uint256[]) private _applicantPatents;
    mapping(bytes32 => uint256) private _documentHashToPatentId;
    mapping(string => bool) private _usedTitles;
    
    // Security and audit variables
    mapping(uint256 => mapping(address => bool)) private _patentApprovals;
    mapping(uint256 => uint256) private _approvalCount;
    uint256 private _requiredApprovals = 2;
    uint256 private _maxPatentsPerDay = 100;
    mapping(uint256 => uint256) private _dailySubmissionCount;

    // Events for comprehensive audit trail
    event PatentSubmitted(
        uint256 indexed patentId,
        address indexed applicant,
        string title,
        bytes32 indexed documentHash,
        uint256 timestamp
    );

    event PatentReviewStarted(
        uint256 indexed patentId,
        address indexed examiner,
        uint256 timestamp
    );

    event PatentApproved(
        uint256 indexed patentId,
        address indexed approver,
        uint256 timestamp
    );

    event PatentRejected(
        uint256 indexed patentId,
        address indexed examiner,
        string reason,
        uint256 timestamp
    );

    event PatentPublished(
        uint256 indexed patentId,
        address indexed publisher,
        bytes32 indexed blockchainHash,
        uint256 timestamp
    );

    event PatentRevoked(
        uint256 indexed patentId,
        address indexed revoker,
        string reason,
        uint256 timestamp
    );

    event SecurityAlert(
        string alertType,
        address indexed triggeredBy,
        uint256 patentId,
        uint256 timestamp
    );

    /**
     * @dev Contract constructor
     * @param admin Admin address with full permissions
     */
    constructor(address admin) {
        require(admin != address(0), "LKS: Admin cannot be zero address");

        _grantRole(DEFAULT_ADMIN_ROLE, admin);
        _grantRole(EXAMINER_ROLE, admin);
        _grantRole(REVIEWER_ROLE, admin);
        _grantRole(PUBLISHER_ROLE, admin);
        _grantRole(AUDITOR_ROLE, admin);
    }

    /**
     * @dev Submit a new patent application
     * @param title Patent title
     * @param description Detailed patent description
     * @param claims Array of patent claims
     * @param inventors Array of inventor names
     * @param assignee Patent assignee (optional)
     * @param documentHash Hash of patent documents
     * @param priorArtHash Hash of prior art analysis
     */
    function submitPatent(
        string calldata title,
        string calldata description,
        string[] calldata claims,
        string[] calldata inventors,
        string calldata assignee,
        bytes32 documentHash,
        bytes32 priorArtHash
    ) external nonReentrant whenNotPaused returns (uint256) {
        // Input validation
        require(bytes(title).length > 0 && bytes(title).length <= 200, "LKS: Invalid title length");
        require(bytes(description).length > 0 && bytes(description).length <= 10000, "LKS: Invalid description length");
        require(claims.length > 0 && claims.length <= 50, "LKS: Invalid claims count");
        require(inventors.length > 0 && inventors.length <= 10, "LKS: Invalid inventors count");
        require(documentHash != bytes32(0), "LKS: Document hash required");
        require(priorArtHash != bytes32(0), "LKS: Prior art hash required");
        
        // Security checks
        require(!_usedTitles[title], "LKS: Title already used");
        require(_documentHashToPatentId[documentHash] == 0, "LKS: Document already submitted");
        
        // Daily submission limit check
        uint256 currentDay = block.timestamp / 1 days;
        require(_dailySubmissionCount[currentDay] < _maxPatentsPerDay, "LKS: Daily submission limit exceeded");

        // Create new patent
        _patentIdCounter.increment();
        uint256 patentId = _patentIdCounter.current();

        Patent storage patent = _patents[patentId];
        patent.id = patentId;
        patent.applicant = msg.sender;
        patent.title = title;
        patent.description = description;
        patent.claims = claims;
        patent.inventors = inventors;
        patent.assignee = assignee;
        patent.status = PatentStatus.Submitted;
        patent.submissionDate = block.timestamp;
        patent.documentHash = documentHash;
        patent.priorArtHash = priorArtHash;
        patent.isActive = true;

        // Update mappings
        _applicantPatents[msg.sender].push(patentId);
        _documentHashToPatentId[documentHash] = patentId;
        _usedTitles[title] = true;
        _dailySubmissionCount[currentDay]++;

        emit PatentSubmitted(patentId, msg.sender, title, documentHash, block.timestamp);
        
        return patentId;
    }

    /**
     * @dev Start patent review process
     * @param patentId Patent ID to review
     */
    function startPatentReview(uint256 patentId) 
        external 
        onlyRole(EXAMINER_ROLE) 
        nonReentrant 
        whenNotPaused 
    {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        require(_patents[patentId].status == PatentStatus.Submitted, "LKS: Invalid patent status");
        require(_patents[patentId].isActive, "LKS: Patent not active");

        _patents[patentId].status = PatentStatus.UnderReview;
        _patents[patentId].reviewDate = block.timestamp;
        _patents[patentId].examiner = msg.sender;

        emit PatentReviewStarted(patentId, msg.sender, block.timestamp);
    }

    /**
     * @dev Approve patent (requires multiple approvals)
     * @param patentId Patent ID to approve
     */
    function approvePatent(uint256 patentId) 
        external 
        onlyRole(REVIEWER_ROLE) 
        nonReentrant 
        whenNotPaused 
    {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        require(_patents[patentId].status == PatentStatus.UnderReview, "LKS: Invalid patent status");
        require(_patents[patentId].isActive, "LKS: Patent not active");
        require(!_patentApprovals[patentId][msg.sender], "LKS: Already approved by this reviewer");

        _patentApprovals[patentId][msg.sender] = true;
        _approvalCount[patentId]++;

        emit PatentApproved(patentId, msg.sender, block.timestamp);

        // Check if required approvals reached
        if (_approvalCount[patentId] >= _requiredApprovals) {
            _patents[patentId].status = PatentStatus.Approved;
            _patents[patentId].approvalDate = block.timestamp;
            _patents[patentId].reviewer = msg.sender;
        }
    }

    /**
     * @dev Reject patent with reason
     * @param patentId Patent ID to reject
     * @param reason Rejection reason
     */
    function rejectPatent(uint256 patentId, string calldata reason) 
        external 
        onlyRole(EXAMINER_ROLE) 
        nonReentrant 
        whenNotPaused 
    {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        require(_patents[patentId].status == PatentStatus.UnderReview, "LKS: Invalid patent status");
        require(_patents[patentId].isActive, "LKS: Patent not active");
        require(bytes(reason).length > 0, "LKS: Rejection reason required");

        _patents[patentId].status = PatentStatus.Rejected;
        _patents[patentId].rejectionReason = reason;

        emit PatentRejected(patentId, msg.sender, reason, block.timestamp);
    }

    /**
     * @dev Publish approved patent to blockchain
     * @param patentId Patent ID to publish
     * @param blockchainHash Hash of blockchain publication
     */
    function publishPatent(uint256 patentId, bytes32 blockchainHash) 
        external 
        onlyRole(PUBLISHER_ROLE) 
        nonReentrant 
        whenNotPaused 
    {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        require(_patents[patentId].status == PatentStatus.Approved, "LKS: Patent not approved");
        require(_patents[patentId].isActive, "LKS: Patent not active");
        require(blockchainHash != bytes32(0), "LKS: Blockchain hash required");

        _patents[patentId].status = PatentStatus.Published;
        _patents[patentId].publicationDate = block.timestamp;

        emit PatentPublished(patentId, msg.sender, blockchainHash, block.timestamp);
    }

    /**
     * @dev Revoke patent (emergency function)
     * @param patentId Patent ID to revoke
     * @param reason Revocation reason
     */
    function revokePatent(uint256 patentId, string calldata reason) 
        external 
        onlyRole(DEFAULT_ADMIN_ROLE) 
        nonReentrant 
    {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        require(_patents[patentId].isActive, "LKS: Patent already inactive");
        require(bytes(reason).length > 0, "LKS: Revocation reason required");

        _patents[patentId].status = PatentStatus.Revoked;
        _patents[patentId].isActive = false;

        emit PatentRevoked(patentId, msg.sender, reason, block.timestamp);
        emit SecurityAlert("PATENT_REVOKED", msg.sender, patentId, block.timestamp);
    }

    // View functions for transparency
    function getPatent(uint256 patentId) external view returns (Patent memory) {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        return _patents[patentId];
    }

    function getPatentsByApplicant(address applicant) external view returns (uint256[] memory) {
        return _applicantPatents[applicant];
    }

    function getTotalPatents() external view returns (uint256) {
        return _patentIdCounter.current();
    }

    function getPatentStatus(uint256 patentId) external view returns (PatentStatus) {
        require(_patents[patentId].id != 0, "LKS: Patent does not exist");
        return _patents[patentId].status;
    }

    function getApprovalCount(uint256 patentId) external view returns (uint256) {
        return _approvalCount[patentId];
    }

    function hasApproved(uint256 patentId, address reviewer) external view returns (bool) {
        return _patentApprovals[patentId][reviewer];
    }

    // Admin functions
    function setRequiredApprovals(uint256 required) external onlyRole(DEFAULT_ADMIN_ROLE) {
        require(required > 0 && required <= 10, "LKS: Invalid approval count");
        _requiredApprovals = required;
    }

    function setMaxPatentsPerDay(uint256 maxPatents) external onlyRole(DEFAULT_ADMIN_ROLE) {
        require(maxPatents > 0, "LKS: Max patents must be positive");
        _maxPatentsPerDay = maxPatents;
    }

    function emergencyPause() external onlyRole(DEFAULT_ADMIN_ROLE) {
        _pause();
        emit SecurityAlert("EMERGENCY_PAUSE", msg.sender, 0, block.timestamp);
    }

    function emergencyUnpause() external onlyRole(DEFAULT_ADMIN_ROLE) {
        _unpause();
    }

    function supportsInterface(bytes4 interfaceId) public view virtual override(AccessControl) returns (bool) {
        return super.supportsInterface(interfaceId);
    }
}
